'use strict';
import React from 'react';
import { View, Image, NativeModules, NativeEventEmitter, Text, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';

import ResourceLoader from '../ResourceLoader';
import ContentDescription from './ContentDescription';
import HideableView from './HideableView';
import PlaybackProgressBar from './PlaybackProgressBar';
import InProgressView from './InProgressView';
import PlaybackSettingsView from './PlaybackSettingsView';
import Native from '../Native';
import NotificationPopup from './NotificationPopup';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

export default class PlaybackView extends React.Component {
  constructor(props) {
    super(props);
    this.playbackTimeCurrent = 0;
    this.playbackTimeTotal = 0;
    this.state = {
      selectedIndex: this.props.selectedIndex
    };
    this.visible = false;
    this.keysListenningOff = false;
    this.playbackStarted = false;
    this.playerState = 'Idle';
    this.operationInProgress = false;
    this.inProgressDescription = 'Please wait...';
    this.playbackInfoInterval = -1;
    this.subtitleTextInterval = -1;
    this.onScreenTimeOut = -1;
    this.showingSettingsView = false;
    this.showNotificationPopup = false;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.streamsData = {
      Audio: [],
      Video: [],
      Subtitle: [],
      Teletext: [],
      Count: 4,
      selectedIndex: -1
    };
    this.currentSubtitleText = '';
    this.popupMessage = '';
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.rerender = this.rerender.bind(this);
    this.toggleView = this.toggleView.bind(this);
    this.onPlaybackCompleted = this.onPlaybackCompleted.bind(this);
    this.onPlayerStateChanged = this.onPlayerStateChanged.bind(this);
    this.onUpdateBufferingProgress = this.onUpdateBufferingProgress.bind(this);
    this.onUpdatePlayTime = this.onUpdatePlayTime.bind(this);
    this.resetPlaybackTime = this.resetPlaybackTime.bind(this);
    this.onSeekCompleted = this.onSeekCompleted.bind(this);
    this.onPlaybackError = this.onPlaybackError.bind(this);
    this.handleFastForwardKey = this.handleFastForwardKey.bind(this);
    this.handleRewindKey = this.handleRewindKey.bind(this);
    this.getFormattedTime = this.getFormattedTime.bind(this);
    this.handlePlaybackInfoDisappeard = this.handlePlaybackInfoDisappeard.bind(this);
    this.showPlaybackInfo = this.showPlaybackInfo.bind(this);
    this.stopPlaybackTime = this.stopPlaybackTime.bind(this);
    this.refreshPlaybackInfo = this.refreshPlaybackInfo.bind(this);
    this.setIntervalImmediately = this.setIntervalImmediately.bind(this);
    this.handleSeek = this.handleSeek.bind(this);
    this.handleSettingsViewDisappeared = this.handleSettingsViewDisappeared.bind(this);
    this.onGotStreamsDescription = this.onGotStreamsDescription.bind(this);
    this.onSubtitleSelection = this.onSubtitleSelection.bind(this);
    this.handleNotificationPopupDisappeared = this.handleNotificationPopupDisappeared.bind(this);
    this.stopInProgressAnimation = this.stopInProgressAnimation.bind(this);
    this.resetPlaybackState = this.resetPlaybackState.bind(this);
  }
  componentWillMount() {
    DeviceEventEmitter.addListener("PlaybackView/onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.addListener("onPlaybackCompleted", this.onPlaybackCompleted);
    this.JuvoEventEmitter.addListener("onPlayerStateChanged", this.onPlayerStateChanged);
    this.JuvoEventEmitter.addListener("onUpdateBufferingProgress", this.onUpdateBufferingProgress);
    this.JuvoEventEmitter.addListener("onUpdatePlayTime", this.onUpdatePlayTime);
    this.JuvoEventEmitter.addListener("onSeekCompleted", this.onSeekCompleted);
    this.JuvoEventEmitter.addListener("onPlaybackError", this.onPlaybackError);
    this.JuvoEventEmitter.addListener("onGotStreamsDescription", this.onGotStreamsDescription);
    this.resetPlaybackState();
  }
  componentWillUnmount() {
    DeviceEventEmitter.removeListener("PlaybackView/onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.removeListener("onPlaybackCompleted", this.onPlaybackCompleted);
    this.JuvoEventEmitter.removeListener("onPlayerStateChanged", this.onPlayerStateChanged);
    this.JuvoEventEmitter.removeListener("onUpdateBufferingProgress", this.onUpdateBufferingProgress);
    this.JuvoEventEmitter.removeListener("onUpdatePlayTime", this.onUpdatePlayTime);
    this.JuvoEventEmitter.removeListener("onSeekCompleted", this.onSeekCompleted);
    this.JuvoEventEmitter.removeListener("onPlaybackError", this.onPlaybackError);
    this.JuvoEventEmitter.removeListener("onGotStreamsDescription", this.onGotStreamsDescription);
    this.resetPlaybackState();
  }

  getFormattedTime(milisecs) {
    var seconds = parseInt((milisecs / 1000) % 60);
    var minutes = parseInt((milisecs / (1000 * 60)) % 60);
    var hours = parseInt((milisecs / (1000 * 60 * 60)) % 24);
    return '%hours:%minutes:%seconds'
      .replace('%hours', hours.toString().padStart(2, '0'))
      .replace('%minutes', minutes.toString().padStart(2, '0'))
      .replace('%seconds', seconds.toString().padStart(2, '0'));
  }
  resetPlaybackState() {
    this.resetPlaybackTime();
    this.handlePlaybackInfoDisappeard();
    this.playbackStarted = false;
    this.showingSettingsView = false;
    this.playerState = 'Idle';
    this.inProgressDescription = 'Please wait...';
    this.JuvoPlayer.StopPlayback();
  }
  toggleView() {
    if (this.visible) {
      //Executed when the playback view is being closed (returns to the content catalog view).
      //Reseting the playback info screen to make it ready for starting a video playback again.
      this.resetPlaybackState();
    }
    //Manage hide/show state between the content catalog and the playback View
    this.visible = !this.visible;
    this.props.switchView('ContentCatalog');
  }
  handleSeek() {
    if (this.playerState == 'Paused') return false;
    this.operationInProgress = true;
    this.inProgressDescription = 'Seeking...';
    this.showPlaybackInfo();
    return true;
  }
  handleFastForwardKey() {
    if (this.handleSeek()) this.JuvoPlayer.Forward();
  }
  handleRewindKey() {
    if (this.handleSeek()) this.JuvoPlayer.Rewind();
  }
  handlePlaybackInfoDisappeard() {
    this.stopPlaybackTime();
    this.rerender();
  }
  handleSettingsViewDisappeared() {
    this.showingSettingsView = false;
    this.rerender();
  }
  handleNotificationPopupDisappeared() {
    this.showNotificationPopup = false;
    this.rerender();
    this.toggleView();
  }
  onPlaybackCompleted(param) {
    this.toggleView();
  }
  onPlayerStateChanged(player) {
    if (player.State === 'Playing') {
      this.operationInProgress = false;
      this.showPlaybackInfo();
    }
    if (player.State === 'Idle') {
      this.resetPlaybackTime();
      this.rerender();
    }
    this.playerState = player.State;
  }
  onUpdateBufferingProgress(buffering) {
    if (buffering.Percent == 100) {
      this.inProgressDescription = 'Please wait...';
      this.operationInProgress = false;
    } else {
      this.inProgressDescription = 'Buffering...';
      this.operationInProgress = true;
    }
    this.rerender();
  }
  onUpdatePlayTime(playtime) {
    this.playbackTimeCurrent = parseInt(playtime.Current);
    this.playbackTimeTotal = parseInt(playtime.Total);
    this.currentSubtitleText = playtime.SubtiteText;
  }
  onSeekCompleted() {
    this.operationInProgress = false;
    this.inProgressDescription = "Please wait...";
  }
  onPlaybackError(error) {
    this.popupMessage = error.Message;
    this.showNotificationPopup = true;
    this.operationInProgress = false;
    this.showingSettingsView = false;
    this.stopInProgressAnimation();
    this.rerender();
  }
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    if (this.keysListenningOff) return;
    switch (pressed.KeyName) {
      case "Right":
        this.handleFastForwardKey();
        break;
      case "Left":
        this.handleRewindKey();
        break;
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":
        if (this.playerState === "Paused" || this.playerState === "Playing") {
          //pause - resume
          this.JuvoPlayer.PauseResumePlayback();
          this.showPlaybackInfo();
        }
        break;
      case "XF86Back":
      case "XF86AudioStop":
        if (this.playbackInfoInterval == -1) {
          this.toggleView();
        } else {
          this.stopPlaybackTime();
        }
        break;
      case "Up":
        //Show the settings view only if the playback controls are visible on the screen
        if (this.onScreenTimeOut >= 0) {
          //requesting the native module for details regarding the stream settings.
          //The response is handled inside the onGotStreamsDescription() function.
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Audio);
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Video);
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Subtitle);
        }
      default:
        this.showPlaybackInfo();
    }
  }
  onGotStreamsDescription(streams) {
    var StreamType = Native.JuvoPlayer.Common.StreamType;
    switch (streams.StreamTypeIndex) {
      case StreamType.Audio:
        this.streamsData.Audio = JSON.parse(streams.Description);
        break;
      case StreamType.Video:
        this.streamsData.Video = JSON.parse(streams.Description);
        break;
      case StreamType.Subtitle:
        this.streamsData.Subtitle = JSON.parse(streams.Description);
        break;
    }
    this.showingSettingsView = this.streamsData.Audio !== null && this.streamsData.Video !== null && this.streamsData.Subtitle !== null;
    if (this.showingSettingsView) this.rerender();
  }
  onSubtitleSelection(Selected) {
    if (this.subtitleTextInterval >= 0) {
      clearInterval(this.subtitleTextInterval);
      this.subtitleTextInterval = -1;
    }
    if (Selected != "off") {
      this.subtitleTextInterval = this.setIntervalImmediately(this.rerender, 100);
    } else {
      this.currentSubtitleText = "";
      this.rerender();
    }
  }
  showPlaybackInfo() {
    this.stopPlaybackTime();
    this.refreshPlaybackInfo();
  }
  resetPlaybackTime() {
    this.playbackTimeCurrent = 0;
    this.playbackTimeTotal = 0;
  }
  stopPlaybackTime() {
    if (this.playbackInfoInterval >= 0) {
      clearInterval(this.playbackInfoInterval);
      this.playbackInfoInterval = -1;
      this.stopInProgressAnimation();
    }
  }
  stopInProgressAnimation() {
    clearTimeout(this.onScreenTimeOut);
    this.onScreenTimeOut = -1;
  }
  refreshPlaybackInfo() {
    this.onScreenTimeOut = setTimeout(this.handlePlaybackInfoDisappeard, 10000);
    this.playbackInfoInterval = this.setIntervalImmediately(this.rerender, 500);
  }
  setIntervalImmediately(func, interval) {
    func();
    return setInterval(func, interval);
  }
  rerender() {
    this.setState({
      selectedIndex: this.state.selectedIndex
    });
  }
  render() {
    const index = this.state.selectedIndex;
    this.streamsData.selectedIndex = index;
    const title = ResourceLoader.clipsData[index].title;
    const fadeduration = 300;
    const revIconPath = ResourceLoader.playbackIconsPathSelect("rew");
    const ffwIconPath = ResourceLoader.playbackIconsPathSelect("ffw");
    const settingsIconPath = ResourceLoader.playbackIconsPathSelect("set");
    const playIconPath = this.playerState !== "Playing" ? ResourceLoader.playbackIconsPathSelect("play") : ResourceLoader.playbackIconsPathSelect("pause");
    const visibility = this.props.visibility ? this.props.visibility : this.visible;
    this.visible = visibility;

    if (this.playerState === "Idle" && this.visible && !this.playbackStarted) {
      const video = ResourceLoader.clipsData[this.state.selectedIndex];
      let DRM = video.drmDatas ? JSON.stringify(video.drmDatas) : null;
      this.JuvoPlayer.StartPlayback(video.url, DRM, video.type);
      this.playbackStarted = true;
      this.operationInProgress = true;
    }

    this.keysListenningOff = !visibility || this.showingSettingsView || this.showNotificationPopup;
    const total = this.playbackTimeTotal;
    const current = this.playbackTimeCurrent;
    const playbackTime = total > 0 ? current / total : 0;
    const progress = Math.round(playbackTime * 100) / 100;
    const subtitleText = this.currentSubtitleText;
    var subtitlesStyle = subtitleText == '' ? [styles.subtitles, { opacity: 0 }] : [styles.subtitles, { opacity: 0.8 }];

    return (
      <View style={{ position: 'absolute', width: width, height: height }}>
        <HideableView position={'absolute'} visible={visibility} duration={fadeduration} height={height} width={width}>
          <View style={[styles.page, { justifyContent: 'flex-end' }]}>
            <View style={[subtitlesStyle, { paddingBottom: height / 4.8 }]}>
              <Text style={styles.textSubtitles}>{subtitleText}</Text>
            </View>
          </View>
          <HideableView position={'relative'} visible={this.onScreenTimeOut >= 0} duration={fadeduration} height={height} width={width}>
            <View style={[styles.page, { position: 'relative' }]}>
              <View style={[styles.transparentPage, { flex: 2, flexDirection: 'row' }]}>
                <View style={[styles.element, { flex: 1 }]} />
                <View style={[styles.element, { flex: 8 }]}>
                  <ContentDescription viewStyle={styles.element} headerStyle={styles.textHeader} bodyStyle={styles.textBody} headerText={title} bodyText={''} />
                </View>
                <View style={[styles.element, { flex: 1 }]}>
                  <Image resizeMode='cover' style={styles.icon} source={settingsIconPath} />
                </View>
              </View>
              <View style={[styles.element, { flex: 8, justifyContent: 'flex-end' }]} />
              <View style={[styles.transparentPage, { flex: 2 }]}>
                <View style={[styles.element, { flex: 2, justifyContent: 'flex-start' }]}>
                  <PlaybackProgressBar value={progress} color='green' />
                  <Text style={[styles.time, { alignSelf: 'flex-start', marginLeft: 50 }]}>{this.getFormattedTime(this.playbackTimeCurrent)}</Text>
                  <Text style={[styles.time, { alignSelf: 'flex-end', marginLeft: 50 }]}>{this.getFormattedTime(this.playbackTimeTotal)}</Text>
                </View>
                <View style={{ flex: 5, backgroundColor: 'transparent', flexDirection: 'row' }}>
                  <View style={[styles.element, { flex: 1 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={revIconPath} />
                  </View>
                  <View style={[styles.element, { flex: 10 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={playIconPath} />
                  </View>
                  <View style={[styles.element, { flex: 1 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={ffwIconPath} />
                  </View>
                </View>
              </View>
            </View>
          </HideableView>
          <View style={[styles.page, styles.element]}>
            <PlaybackSettingsView
              visible={this.showingSettingsView}
              onCloseSettingsView={this.handleSettingsViewDisappeared}
              onSubtitleSelection={this.onSubtitleSelection}
              streamsData={this.streamsData}
            />
          </View>
          <View style={[styles.page, styles.element]}>
            <InProgressView visible={this.operationInProgress} message={this.inProgressDescription} />
          </View>
          <View style={[styles.page, styles.element]}>
            <NotificationPopup visible={this.showNotificationPopup} onNotificationPopupDisappeared={this.handleNotificationPopupDisappeared} messageText={this.popupMessage} />
          </View>
        </HideableView>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  page: {
    position: 'absolute',
    backgroundColor: 'transparent',
    height: height,
    width: width,
  },
  element: {
    backgroundColor: 'transparent',
    alignItems: 'center',
    justifyContent: 'center',
  },
  icon: {
    width: 70,
    height: 70,
  },
  time: {
    marginTop: 25,
    position: 'absolute',
    width: 150,
    height: 30,
    fontSize: 30,
    color: "white",
  },
  transparentPage: {
    backgroundColor: 'black',
    opacity: 0.9,
  },
  textHeader: {
    fontSize: 60,
    color: "white",
    alignSelf: "center",
  },
  textBody: {
    fontSize: 30,
    color: "white",
  },
  textSubtitles: {
    fontSize: 30,
    color: "white",
    textAlign: "center",
    backgroundColor: "black"
  },
  subtitles: {
    width: width,
    height: 150,
  }
});
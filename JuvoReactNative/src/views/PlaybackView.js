'use strict'
import React from 'react';
import {  
  View,
  Image,
  NativeModules,
  NativeEventEmitter,  
  Text
} from 'react-native';

import ResourceLoader from '../ResourceLoader';
import ContentDescription from  './ContentDescription';
import HideableView from './HideableView';
import PlaybackProgressBar from './PlaybackProgressBar';
import InProgressView from './InProgressView';
import PlaybackSettingsView from './PlaybackSettingsView';
import Native from '../Native';
import NotificationPopup from './NotificationPopup';

export default class PlaybackView extends React.Component {
  constructor(props) {
    super(props);   
    this.curIndex = 0;
    this.playbackTimeCurrent = 0;    
    this.playbackTimeTotal = 0;     
    this.state = {        
        selectedIndex: 0
      };      
    this.visible =  this.props.visibility ? this.props.visibility : false;     
    this.keysListenningOff = false;    
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
    this.onSeek = this.onSeek.bind(this);  
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
    this.hideInProgressAnimation = this.hideInProgressAnimation.bind(this);
    this.resetPlaybackState = this.resetPlaybackState.bind(this);
  }   
  componentWillMount() {
    this.JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );       
    this.JuvoEventEmitter.addListener(
      'onPlaybackCompleted',
      this.onPlaybackCompleted
    );
    this.JuvoEventEmitter.addListener(
      'onPlayerStateChanged',
      this.onPlayerStateChanged
    );
    this.JuvoEventEmitter.addListener(
      'onUpdateBufferingProgress',
      this.onUpdateBufferingProgress
    );
    this.JuvoEventEmitter.addListener(
      'onUpdatePlayTime',
      this.onUpdatePlayTime
    );
    this.JuvoEventEmitter.addListener(
      'onSeek',
      this.onSeek
    );      
    this.JuvoEventEmitter.addListener(
      'onPlaybackError',
    this.onPlaybackError
    );  
    this.JuvoEventEmitter.addListener(
      'onGotStreamsDescription',
      this.onGotStreamsDescription
    );   
  }	
  componentWillReceiveProps(nextProps) {        
    this.operationInProgress = nextProps.visibility;
  }
  getFormattedTime(milisecs) {  
    var seconds = parseInt((milisecs/1000)%60)
    var minutes = parseInt((milisecs/(1000*60))%60)
    var hours = parseInt((milisecs/(1000*60*60))%24);
    return "%hours:%minutes:%seconds"
      .replace('%hours', hours.toString().padStart(2, '0'))
      .replace('%minutes', minutes.toString().padStart(2, '0'))
      .replace('%seconds', seconds.toString().padStart(2, '0'))      
  }    
  resetPlaybackState() {
    this.resetPlaybackTime();       
    this.handlePlaybackInfoDisappeard();
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
    this.props.switchView('PlaybackView', this.visible);  
  }   
  handleSeek() {
    if (this.playerState =='Paused') return false; 
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
  handleSettingsViewDisappeared(playbackSettings) {  
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
        this.inProgressDescription = 'Buffering...'
        this.operationInProgress = true;        
      }
      this.rerender();
  }
  onUpdatePlayTime(playtime) {      
    this.playbackTimeCurrent = parseInt(playtime.Current);
    this.playbackTimeTotal = parseInt(playtime.Total);   
    this.currentSubtitleText = playtime.SubtiteText;
  }
  onSeek(time) {       
    this.operationInProgress = false;  
    this.inProgressDescription = 'Please wait...';
  }
  onPlaybackError(error) {   
    this.popupMessage = error.Message;
    this.showNotificationPopup = true;
    this.operationInProgress = false; 
    this.hideInProgressAnimation();
    this.rerender();    
  }
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode      
    if (this.keysListenningOff) return;        
    const video = ResourceLoader.clipsData[this.props.selectedIndex];
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
        if (this.playerState === 'Idle') {                  
          let licenseURI = video.drmDatas ? video.drmDatas[0].licenceUrl : null;
          let DRM = video.drmDatas ? video.drmDatas[0].scheme : null;          
          this.JuvoPlayer.StartPlayback(video.url, licenseURI, DRM, video.type);
        }
        if (this.playerState === 'Paused' || this.playerState === 'Playing') {
          //pause - resume               
          this.JuvoPlayer.PauseResumePlayback();           
          this.showPlaybackInfo();                            
        }                
        break;        
      case "XF86Back":
      case "XF86AudioStop":     
        this.toggleView(); 
        break; 
      case "Up" :
          //requesting the native module for details regarding the stream settings.
          //The response is handled inside the onGotStreamsDescription() function.
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Audio); 
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Video); 
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Subtitle);                
        break;
    }       
  }  
  onGotStreamsDescription(streams) {    
    var StreamType = Native.JuvoPlayer.Common.StreamType;  
    switch (streams.StreamTypeIndex) {
      case StreamType.Audio :
          this.streamsData.Audio = JSON.parse(streams.Description);
        break;
      case StreamType.Video :
          this.streamsData.Video = JSON.parse(streams.Description);
        break;
      case StreamType.Subtitle :
          this.streamsData.Subtitle = JSON.parse(streams.Description); 
        break;      
    } 
    this.showingSettingsView = (this.streamsData.Audio !== null && this.streamsData.Video !== null && this.streamsData.Subtitle !== null); 
  }
  onSubtitleSelection(Selected) {
    if (this.subtitleTextInterval >= 0) {      
      clearInterval(this.subtitleTextInterval);   
      this.subtitleTextInterval = -1;          
    }
    if (Selected != 'off') {
      this.subtitleTextInterval = this.setIntervalImmediately(this.rerender, 100); 
    } else {      
      this.currentSubtitleText = '';
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
      this.hideInProgressAnimation();
    }  
  }  
  hideInProgressAnimation(){
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
    this.setState({selectedIndex: this.state.selectedIndex});    
  }
  render() {    
    const index = this.props.selectedIndex; 
    this.streamsData.selectedIndex = index;
    const title = ResourceLoader.clipsData[index].title;    
    const fadeduration = 300;
    const revIconPath = ResourceLoader.playbackIconsPathSelect('rew');
    const ffwIconPath = ResourceLoader.playbackIconsPathSelect('ffw');
    const settingsIconPath = ResourceLoader.playbackIconsPathSelect('set');   
    const playIconPath = this.playerState !== 'Playing' ? ResourceLoader.playbackIconsPathSelect('play') : ResourceLoader.playbackIconsPathSelect('pause');
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    this.keysListenningOff  = !visibility || this.showingSettingsView || this.showNotificationPopup;        
    const total = this.playbackTimeTotal;
    const current = this.playbackTimeCurrent;   
    const playbackTime = total > 0 ?  current / total : 0;    
    const progress = Math.round((playbackTime) * 100 ) / 100;     
    const subtitleText = this.currentSubtitleText;
    var subtitlesStyle = (subtitleText == '') ? {top: -500, left: 0, width: 1920, height: 150, opacity: 0} : {top: -500, left: 0, width: 1920, height: 150, opacity: 0.8};
    return (
      <View style={{ top: -2680, left: 0, width: 1920, height: 1080}}>          
          <HideableView visible={visibility} duration={fadeduration}>  
            <HideableView visible={this.onScreenTimeOut >= 0} duration={fadeduration}>     
                  <ContentDescription viewStyle={{ top: 0, left: 0, width: 1920, height: 250, justifyContent: 'center', alignSelf: 'center', backgroundColor: '#000000', opacity: 0.8}} 
                                          headerStyle={{ fontSize: 60, color: '#ffffff', alignSelf: 'center', opacity: 1.0}} bodyStyle={{ fontSize: 30, color: '#ffffff', top: 0}} 
                                          headerText={title} bodyText={''}/>
                    <Image resizeMode='cover' 
                        style={{ width: 70 , height: 70, top: -180, left: 1800}} 
                        source={settingsIconPath} 
                      /> 
                  <View style={{ top: 530, left: 0, width: 1920, height: 760,  justifyContent: 'center', alignSelf: 'center', backgroundColor: '#000000', opacity: 0.8}}>
                      <PlaybackProgressBar value={progress}  color="green" />
                      <Image resizeMode='cover' 
                          style={{ width: 70 , height: 70, top: -130, left: 70}} 
                          source={revIconPath} 
                      />
                      <Image resizeMode='cover' 
                          style={{ width: 70 , height: 70, top: -200, left: 930}} 
                          source={playIconPath} 
                      />  
                      <Image resizeMode='cover' 
                          style={{ width: 70 , height: 70, top: -270, left: 1780}} 
                          source={ffwIconPath} 
                      />
                        <Text style={{width: 150 , height: 30, top: -380, left: 60, fontSize: 30, color: '#ffffff' }} >
                          {this.getFormattedTime(this.playbackTimeCurrent)}
                      </Text>
                      <Text style={{width: 150 , height: 30, top: -410, left: 1760, fontSize: 30, color: '#ffffff' }} >
                          {this.getFormattedTime(this.playbackTimeTotal)}
                      </Text>                        
                  </View>                                        
            </HideableView> 
            <View style={{top: -650, left: 870, width: 250, height: 250}}>
              <InProgressView visible={this.operationInProgress} message={this.inProgressDescription} />
            </View>    
            <View style={{top: -1185, left: 180}}>
              <PlaybackSettingsView visible={this.showingSettingsView} 
                                    onCloseSettingsView={this.handleSettingsViewDisappeared}
                                    onSubtitleSelection={this.onSubtitleSelection}
                                    enable={this.showingSettingsView}
                                    streamsData={this.streamsData} />
            </View>      
            <View style = {subtitlesStyle}>
              <Text style={{top: 0, left: 0, fontSize: 30, color: '#ffffff', textAlign:'center', backgroundColor: '#000000' }} >
                  {subtitleText}
              </Text> 
            </View>     
          </HideableView>  
          <View style= {{top: -1300, left: 520, width: 850 , height: 430 }}>
               <NotificationPopup visible={this.showNotificationPopup} 
                                  onNotificationPopupDisappeared={this.handleNotificationPopupDisappeared} 
                                  messageText={this.popupMessage}/>
          </View>             
      </View>
    );
  }
}
'use strict'
import React from 'react';
import {  
  View,
  Image,
  NativeModules,
  NativeEventEmitter,
  ProgressBarAndroid,
  Text
} from 'react-native';

import ResourceLoader from '../ResourceLoader';
import ContentDescription from  './ContentDescription';
import HideableView from './HideableView';
import DisappearingView from './DisappearingView';

export default class PlaybackView extends React.Component {

  constructor(props) {
    super(props);   
    this.curIndex = 0;
    this.state = {        
        selectedIndex: 0
      };      
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.visible =  this.props.visibility ? this.props.visibility : false;     
    this.keysListenningOff = false;    
    this.rerender = this.rerender.bind(this);
    this.toggleView = this.toggleView.bind(this);
    this.onPlaybackCompleted = this.onPlaybackCompleted.bind(this);
    this.onPlayerStateChanged = this.onPlayerStateChanged.bind(this);
    this.onUpdateBufferingProgress = this.onUpdateBufferingProgress.bind(this);
    this.onUpdatePlayTime = this.onUpdatePlayTime.bind(this);
    this.resetPlaybackTime = this.resetPlaybackTime.bind(this);
    this.resetPlaybackTime();   
    this.onSeek = this.onSeek.bind(this);  
    this.onPlaybackError = this.onPlaybackError.bind(this);
    this.handleFastForwardKey = this.handleFastForwardKey.bind(this);
    this.handleRewindKey = this.handleRewindKey.bind(this);
    this.getFormattedTime = this.getFormattedTime.bind(this);
    this.handleViewDisappeard = this.handleViewDisappeard.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.playerState = 'Idle';
  }
  resetPlaybackTime() {
    this.playbackTimeCurrent = 0;
    this.playbackTimeTotal = 0;
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
  componentWillMount() {
    this.JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );
    this.JuvoEventEmitter.addListener(
      'onTVKeyUp',
      this.onTVKeyUp
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
  } 
  shouldComponentUpdate(nextProps, nextState) {  
    return true;
  }
  toggleView() {    
    this.visible = !this.visible; 
    this.playerState = 'Idle';
    this.resetPlaybackTime();
    this.props.switchView('PlaybackView', this.visible);  
  }   
  handleFastForwardKey() { 
    this.JuvoPlayer.forward();
  }
  handleRewindKey() {  
    this.JuvoPlayer.rewind();
  }
  handleViewDisappeard() {    
  }
  onPlaybackCompleted(param) {         
    this.toggleView();
  }
  onPlayerStateChanged(state) {

    this.playerState = state.State;

    if (this.playerState ==='Idle') {      
      this.resetPlaybackTime();
    }
    
    if (this.playerState === 'Playing' || this.playerState === 'Paused') {
      this.rerender(); //just for refreshing the playback controls icons
    }      
    this.JuvoPlayer.log("onPlayerStateChanged... playerState = " +  this.playerState);
  }
  onUpdateBufferingProgress(buffering) {
    this.JuvoPlayer.log("onUpdateBufferingProgress... precent = " + buffering.Percent);
  }
  onUpdatePlayTime(playtime) {
    this.playbackTimeCurrent = parseInt(playtime.Current);
    this.playbackTimeTotal = parseInt(playtime.Total);  
    this.JuvoPlayer.log("onUpdatePlayTime... position = " + this.getFormattedTime(playtime.Current) + ", duration = " + this.getFormattedTime(this.playbackTimeTotal) );
    this.rerender();
  }
  onSeek(time) {
    this.JuvoPlayer.log("onSeek... time = " + time.to);
  }
  onPlaybackError(error) {
    this.JuvoPlayer.log("onPlaybackError message = " + error.Message);
    this.toggleView(); 
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
          this.JuvoPlayer.startPlayback(video.url, licenseURI, DRM, video.type);          
        }
        else {
          //pause         
          this.JuvoPlayer.pauseResumePlayback();                
        }
        break;
      case "XF86Back":
      case "XF86AudioStop":  
        this.JuvoPlayer.stopPlayback(); 
        this.toggleView();        
    }

    this.rerender(); //refreshing the controls view

  }
  rerender() {
    this.setState({selectedIndex: this.state.selectedIndex});    
  }
  render() {    
    const index = this.props.selectedIndex; 
    const title = ResourceLoader.clipsData[index].title;    
    const fadeduration = 500;
    const revIconPath = ResourceLoader.playbackIconsPathSelect('rew');
    const ffwIconPath = ResourceLoader.playbackIconsPathSelect('ffw');
    const settingsIconPath = ResourceLoader.playbackIconsPathSelect('set');   
    const playIconPath = this.playerState !== 'Playing' ? ResourceLoader.playbackIconsPathSelect('play') : ResourceLoader.playbackIconsPathSelect('pause');
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    this.keysListenningOff  = !visibility;    
    const progressbar = this.playbackTimeTotal > 0 ?  this.playbackTimeCurrent / this.playbackTimeTotal : 0;  
    //this.JuvoPlayer.log("PlaybackView render() progressbar =" + progressbar);
    return (
      <View style={{ top: -2680, left: 0, width: 1920, height: 1080}}>
           <HideableView visible={visibility} duration={fadeduration}>    
              <DisappearingView visible={visibility} duration={fadeduration} timeOnScreen={5000} onDisappeared={this.handleViewDisappeard}>     
                    <ContentDescription viewStyle={{ top: 0, left: 0, width: 1920, height: 250, justifyContent: 'center', alignSelf: 'center'}} 
                                            headerStyle={{ fontSize: 60, color: '#ffffff', alignSelf: 'center'}} bodyStyle={{ fontSize: 30, color: '#ffffff', top: 0}} 
                                            headerText={title} bodyText={''}/>
                    <View style={{ top: 350, left: 0, width: 1920, height: 820,  justifyContent: 'center', alignSelf: 'center'}}>
                        <ProgressBarAndroid value={progressbar} style={{left: 0, top: 10, width:1930, height:10}} horizontal={true} color="red" />
                        <Image resizeMode='cover' 
                            style={{ width: 70 , height: 70, top: 70, left: 0}} 
                            source={revIconPath} 
                        />
                        <Image resizeMode='cover' 
                            style={{ width: 70 , height: 70, top: 0, left: 930}} 
                            source={playIconPath} 
                        />  
                        <Image resizeMode='cover' 
                            style={{ width: 70 , height: 70, top: -70, left: 1830}} 
                            source={ffwIconPath} 
                        />
                         <Text style={{width: 150 , height: 30, top: -180, left: 20, fontSize: 30, color: '#ffffff' }} >
                            {this.getFormattedTime(this.playbackTimeCurrent)}
                        </Text>
                        <Text style={{width: 150 , height: 30, top: -210, left: 1800, fontSize: 30, color: '#ffffff' }} >
                            {this.getFormattedTime(this.playbackTimeTotal)}
                        </Text>
                    </View>
                    <Image resizeMode='cover' 
                          style={{ width: 70 , height: 70, top: -1050, left: 1810}} 
                          source={settingsIconPath} 
                        /> 
              </DisappearingView>
          </HideableView>                                       
      </View>
    );
  }
}

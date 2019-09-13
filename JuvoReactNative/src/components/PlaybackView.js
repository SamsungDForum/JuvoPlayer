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
import DisappearingView from './DisappearingView';
import PlaybackProgressBar from './PlaybackProgressBar';

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
    this.seekKeyPressCounter = 0;
    this.seekInProgres = false;
    this.bufferingInProgress = false;
    this.refreshInterval = null;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);  
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
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
    this.handleViewDisappeard = this.handleViewDisappeard.bind(this);
    this.showPlaybackInfo = this.showPlaybackInfo.bind(this);
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

    this.showPlaybackInfo(); 
  }  

  componentWillReceiveProps(nextProps) {   
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
    this.seekInProgres = false;  
    const step = 0.01;  
    this.seekKeyPressCounter += step;   
    this.JuvoPlayer.forward();
  }
  handleRewindKey() {    
    this.seekInProgres = false;   
    const step = 0.01;  
    this.seekKeyPressCounter -= step;
    this.JuvoPlayer.rewind();
  }
  handleViewDisappeard() {    
    if (this.refreshInterval !== null)
        clearInterval(this.refreshInterval);
  }
  onPlaybackCompleted(param) {         
    this.toggleView();
  }
  onPlayerStateChanged(state) {
    this.playerState = state.State;
    if (this.playerState ==='Idle') {      
      this.resetPlaybackTime();
    }     
    this.showPlaybackInfo();    
   // this.JuvoPlayer.log("onPlayerStateChanged... playerState = " +  this.playerState);
  }
  onUpdateBufferingProgress(buffering) {
    this.JuvoPlayer.log("onUpdateBufferingProgress... precent = " + buffering.Percent);
    this.bufferingInProgress = true;
  }
  onUpdatePlayTime(playtime) {
    this.playbackTimeCurrent = parseInt(playtime.Current);
    this.playbackTimeTotal = parseInt(playtime.Total);  
    this.seekInProgres = parseInt(playtime.Seeking) === 1 ? true : false;
    this.bufferingInProgress = false;  
  }
  onSeek(time) {
    this.seekKeyPressCounter = 0;
    this.JuvoPlayer.log("onSeek time is " + time.to);    
    this.seekInProgres = false;      
    this.showPlaybackInfo();  
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
    this.showPlaybackInfo();
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
  }

  onTVKeyUp(pressed) {
    if (this.keysListenningOff) return; 
    this.showPlaybackInfo();
      switch (pressed.KeyName) {
        case "Right":        
        case "Left":   
          this.seekKeyPressCounter = 0;
          this.seekInProgres = false;
        break;
    }
  }

  showPlaybackInfo() {
    this.rerender();
    if (this.refreshInterval !== null)
      clearInterval(this.refreshInterval);    
    this.refreshInterval = setInterval(this.rerender, 1000);
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
    const total = this.playbackTimeTotal;
    const current = this.playbackTimeCurrent;
    const playbackTime = total > 0 ?  current / total : 0;    
    const progress = Math.round((playbackTime + this.seekKeyPressCounter) * 100 ) / 100;   
    const refreshProgresBar = (this.playerState == 'Playing' && !this.bufferingInProgress && !this.seekInProgres) ;
  //  this.JuvoPlayer.log("PlaybackView render() \n seekInProgress =" + this.seekInProgres + 
  //                                            "\n progressbar =" + total + 
 //                                             "\n this.KeyPressCounter =" + this.seekKeyPressCounter +
 //                                             "\n this.bufferingInProgress = " + this.bufferingInProgress);
    return (
      <View style={{ top: -2680, left: 0, width: 1920, height: 1080}}>
           <HideableView visible={visibility} duration={fadeduration}>    
              <DisappearingView visible={visibility} duration={fadeduration} timeOnScreen={7000} onDisappeared={this.handleViewDisappeard}>     
                    <ContentDescription viewStyle={{ top: 0, left: 0, width: 1920, height: 250, justifyContent: 'center', alignSelf: 'center', backgroundColor: '#000000', opacity: 0.8}} 
                                            headerStyle={{ fontSize: 60, color: '#ffffff', alignSelf: 'center', opacity: 1.0}} bodyStyle={{ fontSize: 30, color: '#ffffff', top: 0}} 
                                            headerText={title} bodyText={''}/>
                     <Image resizeMode='cover' 
                          style={{ width: 70 , height: 70, top: -180, left: 1800}} 
                          source={settingsIconPath} 
                        /> 
                    <View style={{ top: 530, left: 0, width: 1920, height: 760,  justifyContent: 'center', alignSelf: 'center', backgroundColor: '#000000', opacity: 0.8}}>
                        <PlaybackProgressBar value={progress}  color="green" doUpdate={refreshProgresBar} />
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
              </DisappearingView>
          </HideableView>                                       
      </View>
    );
  }
}

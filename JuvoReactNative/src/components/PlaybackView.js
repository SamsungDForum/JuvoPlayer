
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

import LocalResources from '../LocalResources';
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
    this.toggleVisibility = this.toggleVisibility.bind(this);
    this.onPlaybackCompleted = this.onPlaybackCompleted.bind(this);
    this.onPlayerStateChanged = this.onPlayerStateChanged.bind(this);
    this.onUpdateBufferingProgress = this.onUpdateBufferingProgress.bind(this);
    this.onUpdatePlayTime = this.onUpdatePlayTime.bind(this);
    this.onSeek = this.onSeek.bind(this);  
    this.handleButtonPressRight = this.handleButtonPressRight.bind(this);
    this.handleButtonPressLeft = this.handleButtonPressLeft.bind(this);
    this.handleViewDisappeard = this.handleViewDisappeard.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.playerState = 'Idle';
  }

  toggleVisibility() {    
    this.visible = !this.visible; 
    this.playerState = 'Idle';
    this.props.switchVisibility('PlaybackView', this.visible);  
  }   

  handleButtonPressRight() { 
  }

  handleButtonPressLeft() {  
  }

  handleViewDisappeard() {   
    this.JuvoPlayer.log("handleViewDisappeard... ");
  }

  onPlaybackCompleted(param) {         
    this.toggleVisibility();
  }
  onPlayerStateChanged(state) {
    this.playerState = state.State;
    
    if (this.playerState === 'Playing' || this.playerState === 'Paused') {
      this.rerender(); //just for refreshing the controls view
    }      
    this.JuvoPlayer.log("onPlayerStateChanged... playerState = " +  this.playerState);
  }
  onUpdateBufferingProgress(percent) {
    this.JuvoPlayer.log("onUpdateBufferingProgress... precent = " + percent.Percent);
  }
  onUpdatePlayTime(position, duration) {
    this.JuvoPlayer.log("onUpdatePlayTime... pos = " + position.CurrentPosition + ", duration = " + duration.Duration );
  }
  onSeek(time) {
    this.JuvoPlayer.log("onSeek... time = " + time.to);

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
  } 

  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode     
    this.JuvoPlayer.log("PlaybacView onTVKeyDown ");
    if (this.keysListenningOff) return;

    this.rerender(); //just for refreshing the view

    const video = LocalResources.clipsData[this.props.selectedIndex];

    switch (pressed.KeyName) {
      case "Right":        
        this.handleButtonPressRight();
        break;
      case "Left":       
        this.handleButtonPressLeft();
        break;
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":      
        if (this.playerState === 'Idle') {
          let licenseURI = video.drmDatas ? video.drmDatas[0].licenceUrl : null;
          let DRM = video.drmDatas ? video.drmDatas[0].scheme : null;          
          this.JuvoPlayer.startPlayback(video.url, licenseURI, DRM);          
        }
        else {
          //pause         
            this.JuvoPlayer.pauseResumePlayback();                
        }
        break;
      case "XF86Back":
      case "XF86AudioStop":
        this.JuvoPlayer.log("onPlayerState is ... " + this.playerState);
        if (this.playerState === 'Playing' || this.playerState === 'Paused') {
            this.JuvoPlayer.stopPlayback();                             
        }                
        this.toggleVisibility();        
    }        
  };  

  onTVKeyUp(pressed) {      
    if (this.keysListenningOff) return; 
  }

  shouldComponentUpdate(nextProps, nextState) {  
    return true;
  } 

  rerender() {
    this.setState({selectedIndex: this.state.selectedIndex});
  }

  render() {    
    const index = this.props.selectedIndex; 
    const title = LocalResources.clipsData[index].title; 
    const description = '';  
    const fadeduration = 500;   
    
    const revIconPath = LocalResources.playbackIconsPathSelect('rew');
    const ffwIconPath = LocalResources.playbackIconsPathSelect('ffw');
    const setIconPath = LocalResources.playbackIconsPathSelect('set');   
    const playIconPath = this.playerState !== 'Playing' ? LocalResources.playbackIconsPathSelect('play') : LocalResources.playbackIconsPathSelect('pause');
    
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    this.keysListenningOff  = !visibility;    
    this.JuvoPlayer.log("PlaybackView render() this.visible = " + this.visible);
    return (
      <View style={{ top: -2680, left: 0, width: 1920, height: 1080}}>
           <HideableView visible={visibility} duration={fadeduration}>    
              <DisappearingView visible={visibility} duration={fadeduration} timeOnScreen={5000} onDisappeared={this.handleViewDisappeard}>     
                    <ContentDescription viewStyle={{ top: 0, left: 0, width: 1920, height: 250, justifyContent: 'center', alignSelf: 'center'}} 
                                            headerStyle={{ fontSize: 60, color: '#ffffff', alignSelf: 'center'}} bodyStyle={{ fontSize: 30, color: '#ffffff', top: 0}} 
                                            headerText={title} bodyText={description}/>
                    <View style={{ top: 350, left: 0, width: 1920, height: 820,  justifyContent: 'center', alignSelf: 'center'}}>
                        <ProgressBarAndroid value={0.8} style={{left: 0, top: 10, width:1930, height:10}} horizontal={true} color="red" />
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
                         <Text style={{width: 100 , height: 30, top: -180, left: 20, fontSize: 30, color: '#ffffff' }} >
                            {'00:00'}
                        </Text>
                        <Text style={{width: 100 , height: 30, top: -210, left: 1830, fontSize: 30, color: '#ffffff' }} >
                            {'23:34'}
                        </Text>
                    </View>
                    <Image resizeMode='cover' 
                            style={{ width: 70 , height: 70, top: -1050, left: 1810}} 
                            source={setIconPath} 
                        /> 
              </DisappearingView>
          </HideableView>                                       
      </View>
    );
  }
}

'use strict'
import React from 'react';
import {  
  View,  
  Text,
  Picker,
  NativeModules,
  NativeEventEmitter
} from 'react-native';

import HideableView from './HideableView';


export default class PlaybackSettingsView extends React.Component {
  constructor(props) {
    super(props);    
    this.playbackSettings = {
        AudioTrack: 0,
        VideoQuality: 0,
        Subtitle: 0
    }
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
       
    this.keysListenningOff = false; 
   
    this.handleConfirmSettings = this.handleConfirmSettings.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
  } 

  componentWillMount() {      
      this.JuvoEventEmitter.addListener(
        'onTVKeyDown',
        this.onTVKeyDown
      );
  }  
    
  componentWillUpdate(nextProps, nextState) {   
    this.keysListenningOff = false;
  }

  handleConfirmSettings() {    
    this.keysListenningOff = true; 
    this.props.onCloseSettingsView();
  }

  onTVKeyDown(pressed) {
    if (this.keysListenningOff) return;  
    switch (pressed.KeyName) {
        case "Right":            
          break;
        case "Left":           
          break;
        case "Return":
        case "XF86AudioPlay":
        case "XF86PlayBack":        
          //this.handleConfirmSettings();               
          break;        
        case "XF86Back":
        case "XF86AudioStop":  
            this.handleConfirmSettings();          
          break;
        case "Up" :
          break;  
      }     
  }
  
  render() {         
    const fadeduration = 300;  
    this.JuvoPlayer.log("this.props.streamsData.Audio.length = " + this.props.streamsData.Audio.length);
    return (
      <View>  
          <HideableView visible={this.props.visible} duration={fadeduration}> 
          <View style={{width: 900, height: 400, paddingTop: 450, justifyContent: 'center', alignItems: 'center', backgroundColor: '#000000', opacity: 0.8}}>          
            <Text style={{left: -200, top: -230, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Audio track
            </Text>
            <Picker
                selectedValue={this.playbackSettings.AudioTrack}
                mode="dropdown"
                style={{left: 100, top: -265, height: 30, width: 350, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {                   
                    this.playbackSettings.AudioTrack = itemValue;                                     
                }                    
                }
                enabled={this.props.enable}>
                {this.props.streamsData.Audio.map((item, index) => {
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>
            <Text style={{left: -200, top: -230, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Video quality
            </Text>
            <Picker
                selectedValue={this.playbackSettings.VideoQuality}
                mode="dropdown"
                style={{left: 100, top: -265, height: 30, width: 350, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                    this.playbackSettings.VideoQuality = itemValue; 
                    }                    
                }
                enabled={this.props.enable}>
                 {this.props.streamsData.Video.map((item, index) => {
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>
            <Text style={{left: -200, top: -230, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Subtitles
            </Text>
            <Picker
                selectedValue={this.playbackSettings.Subtitle}
                mode="dropdown"
                style={{left: 100, top: -265, height: 30, width: 350, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                    this.playbackSettings.Subtitle = itemValue; 
                    }                    
                }
                enabled={this.props.enable}>
                 {this.props.streamsData.Subtitle.map((item, index) => {
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>           
          </View>
          </HideableView>
      </View>
    );
  }
}
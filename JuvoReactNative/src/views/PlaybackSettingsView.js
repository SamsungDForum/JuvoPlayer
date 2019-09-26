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
    this.state = {
        streamsData: -1        
    }
    this.settings = {
        audioSetting: -1,
        videoSetting: -1,
        subtitleSetting: -1
    }
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
       
    this.keysListenningOff = false; 
   
    this.handleConfirmSettings = this.handleConfirmSettings.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.pickerChange = this.pickerChange.bind(this);
   
  } 

  componentWillMount() {      
      this.JuvoEventEmitter.addListener(
        'onTVKeyDown',
        this.onTVKeyDown
      );
  }       

  handleConfirmSettings() {    
    this.keysListenningOff = true;    
    this.props.onCloseSettingsView(this.settings);    
  }

  componentWillReceiveProps(nextProps) {    
    
    const result = (this.state.streamsData.selectedIndex !== nextProps.streamsData.selectedIndex); 
    this.JuvoPlayer.log("nextProps.streamsData = " + result);
    if (result) {
        this.settings = {
            audioSetting: -1,
            videoSetting: -1,
            subtitleSetting: -1
        }

        this.setState({
            streamsData : nextProps.streamsData 
        })       
    
        this.keysListenningOff = false;
    }   
  }  

  shouldComponentUpdate(nextProps, nextState) {
   
    return true;
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
           // this.handleConfirmSettings();               
          break;        
        case "XF86Back":
        case "XF86AudioStop":  
            this.handleConfirmSettings();          
          break;
        case "Up" :
          break;  
      }     
  }

  pickerChange(itemIndex, settingName) {
    this.state.streamsData[settingName].map( (v,i)=>{
        if(itemIndex === i ){            
            switch (settingName) {
                case 'Audio':
                    this.settings.audioSetting =  this.state.streamsData.Audio[itemIndex].Id    
                break;
                case 'Video':
                    this.settings.videoSetting =  this.state.streamsData.Video[itemIndex].Id 
                break;
                case 'Subtitle':
                    this.settings.subtitleSetting = this.state.streamsData.Subtitle[itemIndex].Id  
                break;
            }          
        }
    })
  }
  
  render() {         
    const fadeduration = 300;     
    this.JuvoPlayer.log("this.settings.audioSetting = " + this.settings.audioSetting);
  //  this.JuvoPlayer.log("this.state.videoSetting = " + this.settings.videoSetting);
  //  this.JuvoPlayer.log("this.state.subtitleSetting = " + this.settings.subtitleSetting);
    return (
      <View>  
          <HideableView visible={this.props.visible} duration={fadeduration}> 
          <View style={{width: 1600, paddingTop: 350, justifyContent: 'center', alignItems: 'center', backgroundColor: '#000000', opacity: 0.8}}> 
            <Picker
                selectedValue={this.settings.audioSetting}
                mode="dropdown"
                style={{left: -500, top: -283, height: 30, width: 450, color: '#00ff00'}}
                    onValueChange={(itemValue, itemIndex) => {        
                        this.JuvoPlayer.log("itemValue = " + itemValue);   
                        this.pickerChange(itemIndex, 'Audio');                                                             
                    }                    
                }
                enabled={this.props.enable}>
                {
                    this.props.streamsData.Audio.map((item, index) => {
                        if(item.Default === true && this.settings.audioSetting === -1){   
                            this.settings.audioSetting = item.Id;
                        }
                        return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                    })                    
                }
            </Picker>
            <Text style={{left: -645, top: -360, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Audio track
            </Text>
           
            <Picker
                selectedValue={this.settings.videoSetting}
                mode="dropdown"
                style={{left: 0, top: -350, height: 30, width: 450, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                        this.JuvoPlayer.log("itemValue = " + itemValue);   
                        this.pickerChange(itemIndex, 'Video');      
                    }                    
                }
                enabled={this.props.enable}>
                 {this.props.streamsData.Video.map((item, index) => {
                      if(item.Default === true && this.settings.videoSetting === -1){   
                        this.settings.videoSetting = item.Id;
                    }
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>
            <Text style={{left: -130, top: -428, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Video quality
            </Text>            
            <Picker
                selectedValue={this.settings.subtitleSetting}
                mode="dropdown"
                style={{left: 500, top: -417, height: 30, width: 450, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                    this.JuvoPlayer.log("itemValue = " + itemValue);   
                    this.pickerChange(itemIndex, 'Video');   
                    }                    
                }
                enabled={this.props.enable}>
                 {this.props.streamsData.Subtitle.map((item, index) => {
                    if(item.Default === true && this.settings.subtitleSetting === -1){   
                        this.settings.subtitleSetting = item.Id;
                    }
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker> 
            <Text style={{left: 340, top: -494, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Subtitles
            </Text>          
          </View>
          </HideableView>
      </View>
    );
  }
}
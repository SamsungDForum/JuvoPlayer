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
        streamsData: props.streamsData,
        audioSetting: 0,
        videoSetting: 0,
        subtitleSetting: 0
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
    
  componentWillUpdate(nextProps, nextState) {   
    this.keysListenningOff = false;
  }

  handleConfirmSettings() {    
    this.keysListenningOff = true; 
    this.props.onCloseSettingsView(this.state);
  }

  componentDidUpdate(prevProps, prevState) {  
    if (this.state.streamsData === prevState.streamsData) return false;  
    this.setState({
        streamsData : this.props.streamsData 
     })    
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

  pickerChange(itemIndex, settingName) {
    this.state.streamsData[settingName].map( (v,i)=>{
        if( itemIndex === i ){            
            switch (settingName) {
                case 'Audio':
                    this.setState({
                        audioSetting :  this.state.streamsData.Audio[itemIndex].Id                        
                    })
                break;
                case 'Video':
                    this.setState({
                        videoSetting :  this.state.streamsData.Video[itemIndex].Id                        
                    })
                break;
                case 'Subtitle':
                    this.setState({
                        subtitleSetting :  this.state.streamsData.Subtitle[itemIndex].Id                        
                    })
                break;
            }          
        }
    })
  }
  
  render() {         
    const fadeduration = 300;  
    this.JuvoPlayer.log("this.state.selectedSettings.Audio = " + this.state.audioSetting);
    return (
      <View>  
          <HideableView visible={this.props.visible} duration={fadeduration}> 
          <View style={{width: 900, paddingTop: 350, justifyContent: 'center', alignItems: 'center', backgroundColor: '#000000', opacity: 0.8}}>          
            <Text style={{left: -250, top: -200, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Audio track
            </Text>
            <Picker
                selectedValue={this.state.audioSetting}
                mode="dropdown"
                style={{left: 120, top: -235, height: 30, width: 450, color: '#00ff00'}}
                    onValueChange={(itemValue, itemIndex) => {        
                        this.JuvoPlayer.log("itemValue = " + itemValue);   
                        this.pickerChange(itemIndex, 'Audio');                                                             
                    }                    
                }
                enabled={this.props.enable}>
                {this.props.streamsData.Audio.map((item, index) => {
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>
            <Text style={{left: -240, top: -200, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Video quality
            </Text>
            <Picker
                selectedValue={this.state.videoSetting}
                mode="dropdown"
                style={{left: 120, top: -235, height: 30, width: 450, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                        this.JuvoPlayer.log("itemValue = " + itemValue);   
                        this.pickerChange(itemIndex, 'Video');      
                    }                    
                }
                enabled={this.props.enable}>
                 {this.props.streamsData.Video.map((item, index) => {
                    return (<Picker.Item label={item.Description} value={item.Id} key={index}/>) 
                })}
            </Picker>
            <Text style={{left: -265, top: -200, color: '#00ff00', fontSize: 28, fontWeight: 'bold'}}>
               Subtitles
            </Text>
            <Picker
                selectedValue={this.state.subtitleSetting}
                mode="dropdown"
                style={{left: 120, top: -235, height: 30, width: 450, color: '#00ff00'}}
                onValueChange={(itemValue, itemIndex) => {
                    this.JuvoPlayer.log("itemValue = " + itemValue);   
                    this.pickerChange(itemIndex, 'Video');   
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
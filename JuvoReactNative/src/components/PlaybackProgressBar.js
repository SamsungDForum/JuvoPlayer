'use strict'
import React from 'react';
import {  
  View, 
  NativeModules,
  NativeEventEmitter,
  ProgressBarAndroid  
} from 'react-native';


import PlaybackView from './PlaybackView';

export default class PlaybackProgressBar extends React.Component {

  constructor(props) {
    super(props);     
    this.lastvalue  = 0;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);      
  }

  shouldComponentUpdate(nextProps, nextState) {    
    const oldValue = this.props.value;
    const newValue =  nextProps.value;
    
    this.lastvalue = newValue;
    if ((oldValue !== newValue) && this.props.doUpdate) {   
      return true;
    } else {      
      return false;
    }        
  }

  render() {  
    const value = this.props.value ? this.props.value : this.lastvalue;
    const color = this.props.color ? this.props.color : 'red';
    this.JuvoPlayer.log("PlaybackProgressBar render() this.props.value =" + value);
    return (  
        <View>
            <ProgressBarAndroid style={{left: 10, top: -200, width:1930, height:10}} value={value} horizontal={true} color={color}/>                      
        </View>  
    );
  }
}
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


import PlaybackView from './PlaybackView';

export default class PlaybackProgressBar extends React.Component {

  constructor(props) {
    super(props);     
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);      
  }

  shouldComponentUpdate(nextProps, nextState) {  
    //  return false;
   // return (this.props.value !== nextProps.value);
   if (this.props.value === undefined)  return true;
   const oldValue = 0;//Number(this.props.value).toPrecision(1);
   const newValue = Number(nextProps.value).toPrecision(1);
   this.JuvoPlayer.log("PlaybackProgressBar shouldComponentUpdate() oldValue =" + this.props.value );
    if ((oldValue !== newValue) && this.props.doUpdate)
        return true;
    else
        return false;
  }

  render() {  
    const value = this.props.value ? Number(this.props.value) : 0;
    const color = this.props.color ? this.props.color : 'red';
    this.JuvoPlayer.log("PlaybackProgressBar render() this.props.value =" + this.props.value);
    return (  
        <View>
            <ProgressBarAndroid style={{left: 10, top: -200, width:1930, height:10}} value={value} horizontal={true} color={color}/>                      
        </View>  
    );
  }
}
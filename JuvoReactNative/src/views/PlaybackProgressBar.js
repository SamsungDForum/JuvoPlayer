'use strict';
import React from 'react';
import { View, NativeModules, NativeEventEmitter, ProgressBarAndroid } from 'react-native';

export default class PlaybackProgressBar extends React.Component {
  constructor(props) {
    super(props);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }
  shouldComponentUpdate(nextProps, nextState) {
    const oldValue = this.props.value;
    const newValue = nextProps.value;
    if (oldValue !== newValue) {
      return true;
    } else {
      return false;
    }
  }
  render() {
    const value = this.props.value;
    const color = this.props.color ? this.props.color : 'red';
    return (
      <View>
        <ProgressBarAndroid style={{ width: 1930, height: 10 }} value={value} horizontal={true} color={color} />
      </View>
    );
  }
}

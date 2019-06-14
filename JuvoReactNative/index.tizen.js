/**
 * Sample React Native App
 * https://github.com/facebook/react-native
 * @flow
 */
'use strict'

import React, { Component } from 'react';
import {
  AppRegistry,
  Alert,
  StyleSheet,  
  View,
  Button
} from 'react-native';

var NativeEventEmitter = require('NativeEventEmitter');
import { NativeModules } from 'react-native';
const JuvoPlayer = NativeModules.JuvoPlayer;
var JuvoPlayerEventEmitter = new NativeEventEmitter(JuvoPlayer);
var ProgressBar = require('ProgressBarAndroid');
var TimerMixin = require('react-timer-mixin');

var MovingBar = React.createClass({
  mixins: [TimerMixin],

  getInitialState: function() {
    return {
      progress: 0
    };
  },

  componentDidMount: function() {
    this.setInterval(
      () => {
        var progress = (this.state.progress + 0.02) % 1;
        this.setState({progress: progress});
      }, 50
    );
  },

  render: function() {
    return <ProgressBar progress={this.state.progress} {...this.props} />;
  },
});

const onButtonPress = () => {
  try {
    JuvoPlayer.startPlayback();    
  } catch (e) {
    Alert.alert('Error! ' + e);
  }
};

export default class JuvoReactNative extends Component {  
  render() {
    return (
      <View style={{width:1920, height: 1080}}>
        <Button style={{width:300, height: 100}}
        onPress={onButtonPress}
        title="Start!"
        accessibilityLabel="See an informative alert"
        />
         <MovingBar horizontal={true} style={{top:850, width:1860, height:40, backgroundColor: 'green', color: 'blue'}} />
      </View>
    );
  }
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',    
    backgroundColor: "transparent",
  },
  welcome: {
    fontSize: 20,
    textAlign: 'center',
    margin: 10,
  },
  instructions: {
    textAlign: 'center',
    color: '#333333',
    marginBottom: 5,
  },
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

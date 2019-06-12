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

import { NativeModules } from 'react-native';
const MPlayer = NativeModules.JuvoPlayer;

const onButtonPress = () => {
  try {
    MPlayer.startPlayback();    
  } catch (e) {
    Alert.alert('Error! ' + e);
  }
};

const onHideShowButtonPress = () => {
  try {
    Alert.alert('onHideShowButtonPress');
  } catch (e) {
    Alert.alert('Error! ' + e);
  }
};

export default class JuvoReactNative extends Component {  
  render() {
    return (    
      <View style={{width:300, height: 200, backgroundColor: 'red'}}>
        <Button style={{width:300, height: 100}}
        onPress={onButtonPress}
        title="Start!"
        accessibilityLabel="See an informative alert"
        />
        <Button style={{width:300, height: 100}}
        onPress={onHideShowButtonPress}
        title="Hide/Show"
        accessibilityLabel="See an informative alert"
        />        
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

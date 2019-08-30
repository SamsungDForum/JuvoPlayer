/**
 * Juvo React Native App
 * https://github.com/facebook/react-native
 * @flow
 */
'use strict'
import React, { Component } from 'react';
import {
  StyleSheet,
  View,  
  AppRegistry,
  NativeModules,
  NativeEventEmitter
} from 'react-native';

import ContentCatalog from './src/components/ContentCatalog';

export default class JuvoReactNative extends Component {
  
  constructor(props) {
    super(props);
    this.selectedClipIndex = 0;
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  handleSelectedIndexChange(index) {     
    this.selectedClipIndex = index;
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  }
  
  render() {
    return (
      <View style={{position: 'absolute', backgroundColor: 'transparent',  width: 1920, height: 1080}}>
       <ContentCatalog styles={styles} onSelectedIndexChange={this.handleSelectedIndexChange}/>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    backgroundColor: 'transparent',
    width: 1920,
    height: 1080  
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

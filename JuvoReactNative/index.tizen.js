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
import PlaybackControls from './src/components/PlaybackControls';

export default class JuvoReactNative extends Component {
  
  constructor(props) {
    super(props);    
    this.state = {
      components : {
        'isContentCatalogVisible': true,
        'isPlaybackControlsVisible': false  
      }
    }
    this.selectedClipIndex = 0;
    this.handleComponentsVisibility = this.handleComponentsVisibility.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  } 

  handleComponentsVisibility(componentName, visible) {       
    switch (componentName) {
        case 'ContentCatalog':            
              this.setState({components: {
                'isContentCatalogVisible': visible,
                'isPlaybackControlsVisible': !visible
              }});              
          break;
        case 'PlaybackControls':           
              this.setState({components: {
                'isContentCatalogVisible': !visible,
                'isPlaybackControlsVisible': visible
              }});              
          break;
    }
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  }

  handleSelectedIndexChange(index) {      
    this.selectedClipIndex = index;
  }
  
  render() {   
    this.JuvoPlayer.log("JuvoReactNative render() this.state.components.isContentCatalogVisible = " + this.state.components.isContentCatalogVisible);
    return (
      <View style={styles.container}>        
       <ContentCatalog styles={styles}                    
                       visibility={this.state.components.isContentCatalogVisible}
                       switchVisibility={this.handleComponentsVisibility}
                       onSelectedIndexChange={this.handleSelectedIndexChange}/>
       <PlaybackControls visibility={this.state.components.isPlaybackControlsVisible}
                         switchVisibility={this.handleComponentsVisibility}
                         selectedIndex={this.selectedClipIndex} />
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

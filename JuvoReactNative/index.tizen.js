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
    this.onVisibilityChange = this.onVisibilityChange.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  } 

  onVisibilityChange(componentName, visible) {   
   // this.JuvoPlayer.log("onVisibilityChange componentName = " + componentName + ", visibilityState = " + visible);    
    switch (componentName) {
        case 'ContentCatalog':            
              this.setState({components: {
                'isContentCatalogVisible': !visible,
                'isPlaybackControlsVisible': visible
              }});  
            //  this.JuvoPlayer.log("onVisibilityChange isPlaybackControlsVisible = " + this.state.components.isPlaybackControlsVisible);
          break;
        case 'PlaybackControls':           
              this.setState({components: {
                'isContentCatalogVisible': !visible,
                'isPlaybackControlsVisible': visible
              }}); 
             // this.JuvoPlayer.log("onVisibilityChange isContentCatalogVisible = " + this.state.components.isContentCatalogVisible);
          break;
    }
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  }

  handleSelectedIndexChange(index) {      
    this.selectedClipIndex = index;    
   //this.setState({selectedClipIndex: index});     
  }
  
  render() {
   // this.JuvoPlayer.log("JuvoReactNative render() this.state.components.isContentCatalogVisible = " + this.state.components.isContentCatalogVisible);
    this.JuvoPlayer.log("JuvoReactNative render() this.state.components.isContentCatalogVisible = " + this.state.components.isContentCatalogVisible);
    return (
      <View style={styles.container}>        
       <ContentCatalog styles={styles} 
                      keysListenningOff={this.state.components.isPlaybackControlsVisible}                     
                      visibility={this.state.components.isContentCatalogVisible}
                      onVisibilityChange={this.onVisibilityChange}
                      onSelectedIndexChange={this.handleSelectedIndexChange}/>
       <PlaybackControls  keysListenningOff={!this.state.components.isPlaybackControlsVisible}      
                          selectedIndex={this.selectedClipIndex}                         
                          visibility={this.state.components.isPlaybackControlsVisible}
                          onVisibilityChange={this.onVisibilityChange}/>
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

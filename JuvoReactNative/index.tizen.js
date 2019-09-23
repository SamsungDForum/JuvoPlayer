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
  NativeEventEmitter,
  Text
} from 'react-native';

import ContentCatalog from './src/components/ContentCatalog';
import PlaybackView from './src/components/PlaybackView';
import InProgressView from './src/components/ProgressCircle';

export default class JuvoReactNative extends Component {
  
  constructor(props) {
    super(props);    
    this.state = {
      components : {
        'isContentCatalogVisible': true,
        'isPlaybackViewVisible': false,
        'isInProgressViewVisible': true  
      }
    }
    this.selectedClipIndex = 0;
    this.switchComponentsView = this.switchComponentsView.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  } 
    
  //It is assumed that at the only one component can be visible on the screen
  switchComponentsView(componentName, visible) {       
    switch (componentName) {
        case 'ContentCatalog':            
              this.setState({components: {
                'isContentCatalogVisible': visible,
                'isPlaybackViewVisible': !visible
              }});              
          break;
        case 'PlaybackView':           
              this.setState({components: {
                'isContentCatalogVisible': !visible,
                'isPlaybackViewVisible': visible
              }});              
          break;
        case 'InProgressView':           
          this.setState({components: {
            'isInProgressViewVisible': visible
          }});              
          break;
    }
  }

  handleSelectedIndexChange(index) {      
    this.selectedClipIndex = index;
  }
  
  render() {      
    return (
      <View style={styles.container}>        
       <ContentCatalog styles={styles}                    
                       visibility={this.state.components.isContentCatalogVisible}
                       switchView={this.switchComponentsView}
                       onSelectedIndexChange={this.handleSelectedIndexChange}/>
       <PlaybackView visibility={this.state.components.isPlaybackViewVisible}
                         switchView={this.switchComponentsView}
                         selectedIndex={this.selectedClipIndex} />
        <InProgressView visibility={this.state.components.isInProgressViewVisible}/>
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

/**
 * Juvo React Native App
 * https://github.com/facebook/react-native
 * @flow
 */

'use strict'
import React, {Component} from 'react';
import {AppRegistry, NativeEventEmitter, NativeModules, StyleSheet, View} from 'react-native';

import ContentCatalog from './src/views/ContentCatalog';
import PlaybackView from './src/views/PlaybackView';
import ResourceLoader from "./src/ResourceLoader";
import InProgressView from "./src/views/InProgressView";

export default class JuvoReactNative extends Component {
  constructor(props) {
    super(props);
    this.state = {
      loading: true,
      components: {
        'isContentCatalogVisible': true,
        'isPlaybackViewVisible': false
      },
      deepLinkIndex: 0
    };
    this.selectedClipIndex = 0;
    this.switchComponentsView = this.switchComponentsView.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  componentWillMount() {
    this.JuvoEventEmitter.addListener("handleDeepLink", this.handleDeepLink);
    this.JuvoPlayer.AttachDeepLinkListener();
  }

//It is assumed that at the only one component can be visible on the screen
  switchComponentsView(componentName) {
    switch (componentName) {
      case 'ContentCatalog':
        this.setState({
          components: {
            'isContentCatalogVisible': true,
            'isPlaybackViewVisible': false
          }
        });
        break;
      case 'PlaybackView':
        this.setState({
          components: {
            'isContentCatalogVisible': false,
            'isPlaybackViewVisible': true
          }
        });
        break;
    }
  }

  handleSelectedIndexChange(index) {
    this.selectedClipIndex = index;
  }

  handleDeepLink(deepLink) {
    if (deepLink.url !== null) {
      let index = ResourceLoader.clipsData.findIndex(e => e.url === deepLink.url);
      if (index !== -1) {
        this.setState({deepLinkIndex: index});
        this.handleSelectedIndexChange(this.state.deepLinkIndex);
        this.switchComponentsView('PlaybackView');
      }
    }

    this.setState({loading: false});
  }

  render() {
    if (this.state.loading) {
      return (
        <View style={{ height: "100%", justifyContent: "center", alignItems: "center", backgroundColor: "black"}}>
          <InProgressView visible={true} message="Please wait..."/>
        </View>
      );
    } else {
      return (
        <View style={styles.container}>
          <ContentCatalog styles={styles}
                          visibility={this.state.components.isContentCatalogVisible}
                          switchView={this.switchComponentsView}
                          onSelectedIndexChange={this.handleSelectedIndexChange}
                          deepLinkIndex={this.state.deepLinkIndex}/>

          <PlaybackView visibility={this.state.components.isPlaybackViewVisible}
                        switchView={this.switchComponentsView}
                        selectedIndex={this.selectedClipIndex}/>

        </View>
      );
    }
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

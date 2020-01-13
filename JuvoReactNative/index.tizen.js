/**
 * Juvo React Native App
 * https://github.com/facebook/react-native
 * @flow
 */

'use strict'
import React, {Component} from 'react';
import {
  StyleSheet,
  View,
  AppRegistry,
  NativeModules,
  NativeEventEmitter,
  Dimensions,
  NavigationExperimental,
  DeviceEventEmitter
} from 'react-native';

import ContentCatalog from './src/views/ContentCatalog';
import PlaybackView from './src/views/PlaybackView';
import ResourceLoader from "./src/ResourceLoader";
import InProgressView from "./src/views/InProgressView";

const {
  CardStack: NavigationCardStack,
  StateUtils: NavigationStateUtils
} = NavigationExperimental;

function createReducer(initialState) {
  return (currentState = initialState, action) => {
    switch (action.type) {
      case 'forward':
        return NavigationStateUtils.forward(currentState);
      case 'back':
        return NavigationStateUtils.back(currentState);
      case 'push':
        return NavigationStateUtils.push(currentState, {key: action.key});
      case 'pop':
        return currentState.index > 0 ?
          NavigationStateUtils.pop(currentState) :
          currentState;
      default:
        return currentState;
    }
  }
}

const NavReducer = createReducer({
  index: 0,
  key: 'App',
  routes: [{key: 'Catalog'}]
});

export default class JuvoReactNative extends Component {
  constructor(props) {
    super(props);
    this.state = {
      loading: true,
      navState: NavReducer(undefined, {}),
      deepLinkIndex: 0,
    };
    this.currentView = 'ContentCatalog';
    this.selectedClipIndex = 0;
    this.switchComponentsView = this.switchComponentsView.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  onTVKeyDown(pressed) {
    DeviceEventEmitter.emit(`${this.currentView}/onTVKeyDown`, pressed);
  }

  onTVKeyUp(pressed) {
    DeviceEventEmitter.emit(`${this.currentView}/onTVKeyUp`, pressed);
  }

  componentWillMount() {
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.addListener("onTVKeyUp", this.onTVKeyUp);
    this.JuvoEventEmitter.addListener("handleDeepLink", this.handleDeepLink);
    this.JuvoPlayer.AttachDeepLinkListener();
  }

//It is assumed that at the only one component can be visible on the screen
  switchComponentsView(componentName) {
    switch (componentName) {
      case 'ContentCatalog':
        this.currentView = 'ContentCatalog';
        this._handleAction({type: 'pop'});
        break;
      case 'PlaybackView':
        this.currentView = 'PlaybackView';
        this._handleAction({type: 'push', key: 'Playback'});
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

  _handleAction(action) {
    const newState = NavReducer(this.state.navState, action);
    if (newState === this.state.navState) {
      return false;
    }
    this.setState({
      navState: newState
    });
    return true;
  }

  handleBackAction() {
    return this._handleAction({type: 'pop'});
  }

  _renderRoute(key) {
    if (key === 'Catalog') {
      return <ContentCatalog
        visibility={true}
        switchView={this.switchComponentsView}
        stateView={this.currentView}
        onSelectedIndexChange={this.handleSelectedIndexChange}
        deepLinkIndex={this.state.deepLinkIndex}/>
    }
    if (key === 'Playback') {
      return <PlaybackView
        visibility={true}
        stateView={this.currentView}
        switchView={this.switchComponentsView}
        selectedIndex={this.selectedClipIndex}/>
    }
  }

  _renderScene(props) {
    const ComponentToRender = this._renderRoute(props.scene.route.key);
    return (
      ComponentToRender
    );
  }

  render() {
    if (this.state.loading) {
      return (
        <View style={{height: "100%", justifyContent: "center", alignItems: "center", backgroundColor: "black"}}>
          <InProgressView visible={true} message="Please wait..."/>
        </View>
      );
    } else {
      return (
        <NavigationCardStack
          cardStyle={{backgroundColor: 'transparent'}}
          navigationState={this.state.navState}
          onNavigate={this._handleAction.bind(this)}
          renderScene={this._renderScene.bind(this)}/>
      );
    }
  }
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    backgroundColor: 'transparent',
    width: Dimensions.get('window').width,
    height: Dimensions.get('window').height,
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

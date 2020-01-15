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
  routes: [{key: 'ContentCatalog'}]
});

export default class JuvoReactNative extends Component {
  constructor(props) {
    super(props);
    this.state = {
      loading: true,
      navState: NavReducer(undefined, {}),
      deepLinkIndex: 0
    };
    this.selectedClipIndex = 0;
    this.switchComponentsView = this.switchComponentsView.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.currentView = this.currentView.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  onTVKeyDown(pressed) {
    DeviceEventEmitter.emit(`${this.currentView()}/onTVKeyDown`, pressed);
  }

  onTVKeyUp(pressed) {
    DeviceEventEmitter.emit(`${this.currentView()}/onTVKeyUp`, pressed);
  }

  componentWillMount() {
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.addListener("onTVKeyUp", this.onTVKeyUp);
    this.JuvoEventEmitter.addListener("handleDeepLink", this.handleDeepLink);
    this.JuvoPlayer.AttachDeepLinkListener();
  }

  currentView()
  {
    return this.state.navState.routes[this.state.navState.index].key;
  }

//It is assumed that at the only one component can be visible on the screen
  switchComponentsView(componentName) {
    switch (componentName) {
      case 'Previous':
        this.handleAction({type: 'pop'});
        break;
      case 'PlaybackView':
        this.handleAction({type: 'push', key: 'PlaybackView'});
        break;
      default:
        alert('Undefined view');
    }
    this.setState({});
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

  handleAction(action) {
    const newState = NavReducer(this.state.navState, action);
    if (newState === this.state.navState) {
      return false;
    }
    this.setState({
      navState: newState
    });
    return true;
  }

  renderRoute(key) {
    if (key === 'ContentCatalog') {
      return <ContentCatalog
        visibility={true}
        switchView={this.switchComponentsView}
        onSelectedIndexChange={this.handleSelectedIndexChange}
        deepLinkIndex={this.state.deepLinkIndex}/>
    }
    if (key === 'PlaybackView') {
      return <PlaybackView
        visibility={true}
        switchView={this.switchComponentsView}
        selectedIndex={this.selectedClipIndex}/>
    }
  }

  renderScene(props) {
    const ComponentToRender = this.renderRoute(props.scene.route.key);
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
          onNavigate={this.handleAction.bind(this)}
          renderScene={this.renderScene.bind(this)}/>
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

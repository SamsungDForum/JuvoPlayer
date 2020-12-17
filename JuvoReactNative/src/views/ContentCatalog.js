'use strict';
import React, { Component } from 'react';
import { View, NativeModules, NativeEventEmitter, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';

import HideableView from './HideableView';
import ContentPicture from './ContentPicture';
import ContentScroll from './ContentScroll';
import ResourceLoader from '../ResourceLoader';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

export default class ContentCatalog extends Component {
  constructor(props) {
    super(props);
    this.state = {
      selectedClipIndex: 0
    };
    this.bigPictureVisible = this.props.visibility;
    this.keysListenningOff = false;
    this.toggleVisibility = this.toggleVisibility.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleBigPicLoadStart = this.handleBigPicLoadStart.bind(this);
    this.handleBigPicLoadEnd = this.handleBigPicLoadEnd.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyUp', this.onTVKeyUp);
  }
  componentDidUpdate(prevProps, prevState) {
    this.bigPictureVisible = true;
  }
  shouldComponentUpdate(nextProps, nextState) {
    return true;
  }
  toggleVisibility() {
    this.props.switchView('PlaybackView');
  }
  rerender() {
    this.setState({
      selectedIndex: this.state.selectedIndex
    });
  }
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode
    DeviceEventEmitter.emit('ContentScroll/onTVKeyDown', pressed);
    if (this.keysListenningOff) return;
    switch (pressed.KeyName) {
      case 'XF86AudioStop':
      case 'Return':
      case 'XF86AudioPlay':
      case 'XF86PlayBack':
        this.toggleVisibility();
        break;
      case 'XF86Back':
        this.JuvoPlayer.ExitApp();
        break;
      case ('Left', 'Right'):
        break;
    }
    if (this.bigPictureVisible) {
      //hide big picture during the fast scrolling (long key press)
      this.bigPictureVisible = false;
      this.rerender();
    }
  }
  onTVKeyUp(pressed) {
    DeviceEventEmitter.emit('ContentScroll/onTVKeyUp', pressed);
    if (this.keysListenningOff) return;
    this.bigPictureVisible = true;
    this.rerender();
  }
  handleSelectedIndexChange(index) {
    this.props.onSelectedIndexChange(index);
    this.setState({
      selectedClipIndex: index
    });
  }
  handleBigPicLoadStart() {}
  handleBigPicLoadEnd() {
    this.bigPictureVisible = true;
  }
  render() {
    const index = this.state.selectedClipIndex ? this.state.selectedClipIndex : 0;
    const path = ResourceLoader.tilePaths[index];
    const overlay = ResourceLoader.contentDescriptionBackground;
    this.keysListenningOff = !this.props.visibility;
    const showBigPicture = this.bigPictureVisible;
    return (
      <HideableView visible={this.props.visibility} duration={300}>
        <View style={[styles.page, { alignItems: 'flex-end' }]}>
          <View style={[styles.cell, { height: '70%', width: '70%' }]}>
            <HideableView visible={showBigPicture} duration={100}>
              <ContentPicture selectedIndex={index} path={path} onLoadEnd={this.handleBigPicLoadEnd} onLoadStart={this.handleBigPicLoadStart} width={'100%'} height={'100%'} />
            </HideableView>
            <ContentPicture position={'absolute'} source={overlay} selectedIndex={index} width={'100%'} height={'100%'} />
          </View>
        </View>
        <View style={[styles.page, { position: 'absolute' }]}>
          <ContentScroll
            onSelectedIndexChange={this.handleSelectedIndexChange}
            contentURIs={ResourceLoader.tilePaths}
            keysListenningOff={this.keysListenningOff}
            deepLinkIndex={this.props.deepLinkIndex}
          />
        </View>
      </HideableView>
    );
  }
}

const styles = StyleSheet.create({
  page: {
    width: width,
    height: height
  },
  cell: {
    backgroundColor: 'black'
  }
});

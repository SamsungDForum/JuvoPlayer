"use strict";
import React from "react";
import { View, Image, ScrollView, NativeModules, NativeEventEmitter } from "react-native";

import ContentPicture from "./ContentPicture";
import ContentDescription from "./ContentDescription";
import ResourceLoader from "../ResourceLoader";

export default class ContentScroll extends React.Component {
  constructor(props) {
    super(props);
    this.curIndex = 0;
    this.state = { selectedIndex: 0 };
    this.numItems = this.props.contentURIs.length;
    this.deepLinkIndex = 0;
    this.scrolloffset = 0;
    this.itemWidth = 454;
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.handleButtonPressRight = this.handleButtonPressRight.bind(this);
    this.handleButtonPressLeft = this.handleButtonPressLeft.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  handleButtonPressRight() {
    if (this.curIndex < this.numItems - 1) {
      this.curIndex++;
      this.scrolloffset = this.curIndex * this.itemWidth;
      this._scrollView.scrollTo({ x: this.scrolloffset, y: 0, animated: true });
    }
    this.setState({ selectedIndex: this.curIndex });
  }

  handleButtonPressLeft() {
    if (this.curIndex > 0) {
      this.curIndex--;
      this.scrolloffset = this.curIndex * this.itemWidth;
      this._scrollView.scrollTo({ x: this.scrolloffset, y: 0, animated: true });
    }
    this.setState({ selectedIndex: this.curIndex });
  }

  componentWillMount() {
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.addListener("onTVKeyUp", this.onTVKeyUp);
  }

  componentWillReceiveProps(nextProps) {
    if (this.deepLinkIndex === nextProps.deepLinkIndex)
      return;

    this.deepLinkIndex = nextProps.deepLinkIndex;
    this.curIndex = this.deepLinkIndex;
    this.scrolloffset = this.curIndex * this.itemWidth;
    this._scrollView.scrollTo({ x: this.scrolloffset, y: 0, animated: false });
    this.setState({ selectedIndex: this.curIndex });
  }

  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    if (this.props.keysListenningOff) return;

    switch (pressed.KeyName) {
      case "Right":
        this.handleButtonPressRight();
        break;
      case "Left":
        this.handleButtonPressLeft();
        break;
    }
  }

  onTVKeyUp(pressed) {
    if (this.props.keysListenningOff) return;

    this.props.onSelectedIndexChange(this.curIndex);
    this.setState({ selectedIndex: this.curIndex });
  }

  shouldComponentUpdate(nextProps, nextState) {
    return nextState.selectedIndex != this.state.selectedIndex;
  }

  render() {
    const index = this.state.selectedIndex;
    const pathFinder = ResourceLoader.tilePathSelect;
    const title = ResourceLoader.clipsData[index].title;
    const description = ResourceLoader.clipsData[index].description;
    const itemWidth = 454;
    const itemHeight = 260;
    const overlayPath = ResourceLoader.playbackIconsPathSelect("play");
    const renderThumbs = (uri, i) => (
      <View key={i}>
        <Image resizeMode='cover' style={{ top: itemHeight / 2 + 35, left: itemWidth / 2 - 25 }} source={overlayPath} />
        <ContentPicture
          source={uri}
          myIndex={i}
          selectedIndex={index}
          path={pathFinder(uri)}
          width={itemWidth - 8}
          height={itemHeight - 8}
          top={4}
          left={4}
          fadeDuration={1}
          stylesThumbSelected={{
            width: itemWidth,
            height: itemHeight,
            top: 0,
            backgroundColor: "transparent",
            opacity: 0.3
          }}
          stylesThumb={{
            width: itemWidth,
            height: itemHeight,
            top: 0,
            backgroundColor: "transparent",
            opacity: 1
          }}
        />
      </View>
    );
    return (
      <View>
        <View
          style={{
            top: 150,
            left: 100,
            width: 900,
            height: 750
          }}>
          <ContentDescription
            viewStyle={{
              top: 0,
              left: 0,
              width: 900,
              height: 800
            }}
            headerStyle={{ fontSize: 60, color: "#ffffff" }}
            bodyStyle={{ fontSize: 30, color: "#ffffff", top: 0 }}
            headerText={title}
            bodyText={description}
          />
        </View>
        <View>
          <ScrollView
            scrollEnabled={false}
            ref={scrollView => {
              this._scrollView = scrollView;
            }}
            automaticallyAdjustContentInsets={false}
            scrollEventThrottle={0}
            horizontal={true}
            showsHorizontalScrollIndicator={false}>
            {this.props.contentURIs.map(renderThumbs)}
          </ScrollView>
        </View>
      </View>
    );
  }
}

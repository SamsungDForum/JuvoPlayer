/**
 * Sample React Native App
 * https://github.com/facebook/react-native
 * @flow
 */
'use strict'
import React, { Component, PropTypes } from 'react';
import {
  StyleSheet,
  View,
  Text,
  ScrollView,
  Image,
  Alert,
  Button,
  Animated,
  AppRegistry
} from 'react-native';

import LocalImages from './src/LocalImages';

var NativeEventEmitter = require('NativeEventEmitter');

const deviceWidth = 250; 
const FIXED_BAR_WIDTH = 1;
const BAR_SPACE = 1;

import { NativeModules } from 'react-native';
const JuvoPlayer = NativeModules.JuvoPlayer;
var JuvoEventEmitter = new NativeEventEmitter(JuvoPlayer);

const PlayVideo = (clip_url) => {
  try {
    Alert.alert('Ok.' + clip_url);
    //JuvoPlayer.startPlayback();   

  } catch (e) {
    Alert.alert('Error! ' + e);
  }
};


class Thumb extends React.Component {
  constructor(props) {
    super(props);     
    
  }

  shouldComponentUpdate(nextProps, nextState) {    
     return true;
  }

  componentDidMount() {
   // //JuvoPlayer.log("componentDidMount" );
  }

  render() {
    const name = this.props.source;
    const width = this.props.width ? this.props.width : 1920;
    const height = this.props.height ? this.props.height : 1080;
    const top = this.props.top ? this.props.top : 0;
    const left = this.props.left ? this.props.left : 0; 
    const position = this.props.position ? this.props.position : 'relative';
    const stylesThumbSelected = this.props.stylesThumbSelected ? this.props.stylesThumbSelected : {width: 460, height: 266};
    const stylesThumb = this.props.stylesThumb ? this.props.stylesThumb : {width: 460, height: 266};
    const path = this.props.path;
    const fadeDuration = this.props.fadeDuration ? this.props.fadeDuration : 500;
   
    const visible = this.props.visible ? this.props.visible : true;
    const onLoadStart = this.props.onLoadStart ? this.props.onLoadStart : () => {};
    const onLoadEnd = this.props.onLoadEnd ? this.props.onLoadEnd : () => {};
  

    if (this.props.selectedIndex == this.props.myIndex) {           
      return (
        <HideableView visible={visible} duration={fadeDuration} >
          <View style={stylesThumbSelected}>
            <Image resizeMode='cover' 
                  style={{ width: width, height: height, top: top, left: left, position: position}} 
                  source={path} 
                  onLoadStart={onLoadStart}
                  onLoadEnd={onLoadEnd} 
            />
          </View>
        </HideableView>
      );
    } else {
      return (
        <HideableView visible={visible} duration={this.props.fadeDuration} >
          <View style={stylesThumb}>
          <Image resizeMode='cover' 
                style={{ width: width , height: height , top: top, left: left, position: position}} 
                source={path} 
                onLoadStart={this.props.onLoadStart}
                onLoadEnd={this.props.onLoadEnd}
            />
          </View>
        </HideableView>
      );
    }
  }
}
var THUMB_URIS = [
  'car',
  'bolid',
  'sintel',
  'oops',
  'car',
  'bolid',
  'sintel',
  'oops'
];

class HorizontalScrollView extends React.Component {

  constructor(props) {
    super(props);
    this._scrollView;
    this.curIndex = 0    
    this.state = { selectedIndex: 0 };
    this.numItems = THUMB_URIS.length;
    this.itemWidth = (FIXED_BAR_WIDTH / this.numItems) - ((this.numItems - 1) * BAR_SPACE);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);    
    this.handleButtonPressRight = this.handleButtonPressRight.bind(this);
    this.handleButtonPressLeft = this.handleButtonPressLeft.bind(this); 
    
  }

  handleButtonPressRight() {       
    if (this.curIndex < this.numItems - 1) {
      this.curIndex++;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });
    } 
    this.setState({ selectedIndex: this.curIndex });
  };

  handleButtonPressLeft() { 
    if (this.curIndex > 0) {
      this.curIndex--;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });
    };
    this.setState({ selectedIndex: this.curIndex });
  };

  componentWillMount() {
    JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );
    JuvoEventEmitter.addListener(
      'onTVKeyUp',
      this.onTVKeyUp
    );
  }
 

  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode   

    switch (pressed.KeyName) {
      case "Right":        
        this.handleButtonPressRight();
        break;
      case "Left":       
        this.handleButtonPressLeft();
        break;
    }    
    //this.props.onSelectedIndexChange(this.state.selectedIndex);
  };  

  onTVKeyUp(pressed) {
  //  JuvoPlayer.log("HorizontalScrollView onTVKeyUp ...");
    //this.setState({ loadInProgress: false });
    this.setState({ selectedIndex: this.curIndex });   
    this.props.onSelectedIndexChange(this.state.selectedIndex);
    
  }

 

  render() {
    const index = this.state.selectedIndex;
    const width = this.props.itemWidth ? this.props.itemWidth : 454;
    const height = this.props.itemHeight ? this.props.itemHeight : 260;      
    const stylesThumbSelected = this.props.stylesThumbSelected ? this.props.stylesThumbSelected : {width: 460, height: 266, backgroundColor: '#ffd700'};
    const stylesThumb = this.props.stylesThumb ? this.props.stylesThumb : {width: 460, height: 266};
    const renderThumbs = (uri, i) => <Thumb key={i} source={uri} myIndex={i} selectedIndex={index}
      path={this.props.onTilePathSelect(uri)}
      width={width} height={height} top={2} left ={2} position={'relative'} visible={true} fadeDuration={1} 
      stylesThumbSelected={stylesThumbSelected} stylesThumb={stylesThumb} 
      />;

    return (
      <View style={{overflow: 'visible'}}>
        <View style={{position: 'relative', top: 400, left: 500, width: 1920, height: 800, zIndex: 200, overflow: 'visible'}}>
          <OverlayText viewStyle={{ position: 'relative', top: 100, left: 100, width: 1920, height: 800, zIndex: 200, overflow: 'visible' }} 
                      headerStyle={{ fontSize: 30, color: '#7fff00' }} 
                      headerText={THUMB_URIS[index]} />
        </View>
        <View>
        <ScrollView
          scrollEnabled={false}
          ref={(scrollView) => { this._scrollView = scrollView; }}
          automaticallyAdjustContentInsets={false}
          scrollEventThrottle={0}
          horizontal={true}
          showsHorizontalScrollIndicator={false}     
           >
          {THUMB_URIS.map(renderThumbs)}          
        </ScrollView>
        </View>                
      </View>
    );
  }
}

export default class JuvoReactNative extends Component {

  constructor(props) {
    super(props);
    this.state = {
      visible: false,
      selectedClipIndex: 0,      
      bigPictureVisible: true
    };
    this.toggle = this.toggle.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.tilePathSelect = this.tilePathSelect.bind(this);
    this.onBigPictureLoadStart = this.onBigPictureLoadStart.bind(this);
    this.onBigPictureLoadEnd = this.onBigPictureLoadEnd.bind(this);
    this.bigPictureFadeDuration = 500;
  }

  toggle() {
    this.setState({
      visible: !this.state.visible
    });
  }

  componentWillMount() {
    JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );
    JuvoEventEmitter.addListener(
      'onTVKeyUp',
      this.onTVKeyUp
    );
  }

  
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode       
   
    this.setState({bigPictureVisible: false});

    switch (pressed.KeyName) {
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":
        //JuvoPlayer.log("Start playback...");
        if (this.state.visible) {
          // JuvoPlayer.startPlayback();
          this.toggle();
        }
        else {
          //pause
          // JuvoPlayer.pauseResumePlayback();
        }
        break;
      case "XF86Back":
      case "XF86AudioStop":
        if (!this.state.visible) {
         //  JuvoPlayer.stopPlayback();
          this.toggle();
        }
        break;
    }   
  }

  onTVKeyUp(pressed) {         
      this.setState({bigPictureVisible: true}); 
  }

  handleSelectedIndexChange(index) {     
    this.setState({ selectedClipIndex: index});      
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  }

  tilePathSelect = name => {
    if (name === null)
      return LocalImages.tiles.default;

    const tileArray = {
      'car': LocalImages.tiles.car,
      'bolid': LocalImages.tiles.bolid,
      'sintel': LocalImages.tiles.sintel,
      'oops': LocalImages.tiles.oops
    };    
    return tileArray[name];
  }

  onBigPictureLoadStart(event) { 
  }

  onBigPictureLoadEnd(event) {  
  }

  render() {
    JuvoPlayer.log("JuvoReactNative render() this.state.selectedClipIndex = " + this.state.bigPictureVisible);
    const index = this.state.selectedClipIndex; 
    const visible = (index == 1);
    const uri = THUMB_URIS[index];
    return (
      <View style={styles.container}>
        <HideableView  visible={this.state.visible} duration={300}>
          <HideableView  visible={this.state.bigPictureVisible} duration={300} style={{zIndex: -100 }}>          
            <View style={{position: 'relative', top: 0, width: 1920, height: 1080, zIndex: -10 }}>
              <Thumb key={index} source={uri} myIndex={index} selectedIndex={index}
                      path={this.tilePathSelect(uri)}
                      width={1920} height={1080} top={0} left = {0} position={'relative'} visible={true} fadeDuration={this.bigPictureFadeDuration} 
                      stylesThumbSelected={{width: 1920, height: 1080}} stylesThumb={{width: 1920, height: 1080}}
                      onLoadStart = {this.onBigPictureLoadStart} onLoadEnd = {this.onBigPictureLoadEnd}/>

            </View>   
          </HideableView>   
          <View style={{position: 'relative', top: -1080, width: 1920, height: 1080, zIndex: 100 }}>
            <HorizontalScrollView stylesThumbSelected={styles.stylesThumbSelected} stylesThumb={styles.stylesThumb} 
            onSelectedIndexChange={this.handleSelectedIndexChange} onTilePathSelect={this.tilePathSelect}  />
          </View>   
        </HideableView> 
      </View>
    );
  }
}

class OverlayText extends Component {

  constructor(props) {
    super(props);     
  }

  shouldComponentUpdate(nextProps, nextState) {
    return true;
  }

  render() {
    this.handleSelectedIndexChange = this.props.onSelectedIndexChange;
    return(
      <View style={this.props.viewStyle}>
        <Text style={this.props.headerStyle}>
          {this.props.headerText}
        </Text>
      </View>
    );
  }
}
class HideableView extends Component {
  constructor(props) {
    super(props);
    this.state = {
      opacity: new Animated.Value(this.props.visible ? 1 : 0)
    }
  }

  animate(show) {
    const duration = this.props.duration ? parseInt(this.props.duration) : 500;
    Animated.timing(
      this.state.opacity, {
        toValue: show ? 1 : 0,
        duration: !this.props.noAnimation ? duration : 0
      }
    ).start();
  }

  shouldComponentUpdate(nextProps) {   
    return true;
  }

  componentWillUpdate(nextProps, nextState) {
    if (this.props.visible !== nextProps.visible) {
      this.animate(nextProps.visible);
    }
  }

  render() {
    if (this.props.removeWhenHidden) {
      return (this.visible && this.props.children);
    }
    return (
      <View>
        <Animated.View style={{ opacity: this.state.opacity }}>
          {this.props.children}
        </Animated.View>
      </View>
    )
  }
}

HideableView.propTypes = {
  visible: PropTypes.bool.isRequired,
  duration: PropTypes.number,
  removeWhenHidden: PropTypes.bool,
  noAnimation: PropTypes.bool
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    backgroundColor: 'transparent',
    width: 1920,
    height: 1080
  },
  thumb: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#cccccc',
    width: 460,
    height: 266
  },
  thumb_selected: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#ffd700',
    width: 460,
    height: 266
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

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

const deviceWidth = 298 //Dimensions.get('window').width / 100
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

  render() {
    const name =  this.props.source;
   // JuvoPlayer.log("thumb props.sorce " + this.props.source);
   // JuvoPlayer.log("thumb name = " + name);      
    if (this.props.selectedIndex == this.props.myIndex) {
      return (
        <View style={styles.thumb_selected}>
          <Image resizeMode='cover' style={{ width: 449, height: 255 }} source={this.props.onTilePathSelect(name)} />
        </View>
      );
    } else {
      return (
        <View style={styles.thumb}>
          <Image resizeMode='cover' style={{ width: 449, height: 255 }} source={this.props.onTilePathSelect(name)} />
        </View>
      );
    }
  }
}
var THUMB_URIS = [
  'carjpg',
  'bolidjpg',
  'sinteljpg',
  'oopsjpg',
  'carjpg',
  'bolidjpg',
  'sinteljpg',
  'oopsjpg'
];

class HorizontalScrollView extends React.Component {

  constructor(props) {
    super(props);

    this._scrollView;
    this.curIndex = 0
    this.state = { selectedIndex: 0 };
    this.numItems = THUMB_URIS.length;
    this.itemWidth = (FIXED_BAR_WIDTH / this.numItems) - ((this.numItems - 1) * BAR_SPACE);

    this.onTVKey = this.onTVKey.bind(this);
    this._handleButtonPressRight = this._handleButtonPressRight.bind(this);
    this._handleButtonPressLeft = this._handleButtonPressLeft.bind(this);
    this.onMomentumScrollEnd = this.onMomentumScrollEnd.bind(this);
  }

  onMomentumScrollEnd = () => {
    JuvoPlayer.log("onMomentumScrollEnd");
  }
  

  _handleButtonPressRight = () => {
     JuvoPlayer.log("curIndex = " + this.curIndex);
    // JuvoPlayer.log("numItems = " + this.numItems);
    this.scroll_inprogress = false;
    if (this.curIndex < this.numItems - 1) {
      this.curIndex++;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });
    }    
    this.props.onSelectedIndexChange(this.curIndex);
    this.setState({ selectedIndex: this.curIndex });
  };

  _handleButtonPressLeft = () => {
     JuvoPlayer.log("curIndex = " + this.curIndex);
    // JuvoPlayer.log("numItems = " + this.numItems);
    this.scroll_inprogress = false;
    if (this.curIndex > 0) {
      this.curIndex--;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });
    };    
    this.props.onSelectedIndexChange(this.curIndex);
    this.setState({ selectedIndex: this.curIndex });
  };

  componentWillMount() {
    JuvoEventEmitter.addListener(
      'onTVKeyPress',
      this.onTVKey
    );
  }

  onTVKey(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode   
    // JuvoPlayer.log("ScrollView clicked...");
    switch (pressed.KeyName) {
      case "Right":
        //JuvoPlayer.log("Right clicked...");
        this._handleButtonPressRight();
        break;
      case "Left":
        // JuvoPlayer.log("Left clicked...");
        this._handleButtonPressLeft();
        break;
    }
    // JuvoPlayer.log("HorizontalScrollView params - KeyName  " + pressed.KeyName + " the code: " + pressed.KeyCode);
  };

  render() {
    const index = this.state.selectedIndex;
    const renderThumbs = (uri, i) => <Thumb key={i} source={uri} myIndex={i} selectedIndex={index} onTilePathSelect={this.props.onTilePathSelect}/>;    
    return (
      <View>
        <ScrollView
          scrollEnabled={false}
          ref={(scrollView) => { this._scrollView = scrollView; }}
          automaticallyAdjustContentInsets={false}
          scrollEventThrottle={16}
          horizontal={true}
          showsHorizontalScrollIndicator={false}          >          
          {THUMB_URIS.map(renderThumbs)}
        </ScrollView>
      </View>
    );
  }
}



export default class JuvoReactNative extends Component {

  constructor(props) {
    super(props);
    this.state = {
      visible: true,
      selectedClipIndex: 0
    };
    this.toggle = this.toggle.bind(this);
    this.onTVKey = this.onTVKey.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.tilePathSelect = this.tilePathSelect.bind(this);
  }

  toggle() {
    this.setState({
      visible: !this.state.visible
    });
  }

  componentWillMount() {
    JuvoEventEmitter.addListener(
      'onTVKeyPress',
      this.onTVKey
    );
  }

  componentDidMount() {    
    //this.toggle();  
  }

  onTVKey(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode   

    switch (pressed.KeyName) {
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":
        JuvoPlayer.log("Start playback...");
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
        // JuvoPlayer.stopPlayback();
          this.toggle();
        }
        break;
    }
    //JuvoPlayer.log("JuvoReactNative params - KeyName  " + pressed.KeyName + " the code: " + pressed.KeyCode);
  }

  handleSelectedIndexChange(index) {
    this.setState({ selectedClipIndex: index });
  }

  shouldComponentUpdate(nextProps, nextState) {
    return true;
  }

  tilePathSelect = name => {
    if (name === null)
      return LocalImages.tiles.default;
  
      const tileArray = {
        'carjpg': LocalImages.tiles.carjpg,
        'bolidjpg': LocalImages.tiles.bolidjpg,
        'sinteljpg': LocalImages.tiles.sinteljpg,
        'oopsjpg': LocalImages.tiles.oopsjpg
      };
      //JuvoPlayer.log("tilePathSelect name = " + name);
      return tileArray[name];
     
  }

  render() {
    JuvoPlayer.log("JuvoReactNative render() this.state.selectedClipIndex = " + this.state.selectedClipIndex);
    const index = this.state.selectedClipIndex;

    return (
      <View style={styles.container}>

        <HideableView visible={this.state.visible}>

          <View>
          <Image style={{ width: 1920, height: 1080 }} source={this.tilePathSelect(THUMB_URIS[index])} />
          </View>

          <View style={{ position: 'absolute', top: 300, left: 200 }}>
            <Text style={{ fontSize: 30, color: '#7fff00' }}>
              {THUMB_URIS[index]}
            </Text>
          </View>

          <View style={{ top: -300 }}>
            <HorizontalScrollView onSelectedIndexChange={this.handleSelectedIndexChange}  onTilePathSelect = {this.tilePathSelect}/>
          </View>

        </HideableView>

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
    //return this.props.visible !== nextProps.visible;
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
    backgroundColor: 'transparent',
    width: 1920,
    height: 1080
  },
  thumb: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#cccccc',
    width: 459,
    height: 260
  },
  thumb_selected: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#ffd700',
    width: 459,
    height: 260
    
  },
  img_thumb: {
    width: '98%',
    height: '98%'
  }

});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

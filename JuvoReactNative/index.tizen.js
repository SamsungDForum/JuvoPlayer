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
    //JuvoPlayer.log("thumb props.selectedURI " + this.props.selectedIndex);
    //JuvoPlayer.log("thumb props.myURI = " + this.props.myIndex);  
    if ( this.props.selectedIndex == this.props.myIndex) {
      return (
        <View style={styles.thumb_selected}>              
          <Image style={styles.img_thumb} source={{ uri: this.props.source }} />
        </View>
      );
    } else {
      return (
        <View style={styles.thumb}>              
          <Image style={styles.img_thumb} source={{ uri: this.props.source }} />
        </View>
      );  
    }    
  }
}
var THUMB_URIS = [
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true'
];


class HorizontalScrollView extends React.Component {

  constructor(props) {
    super(props);
      
    this._scrollView;       
    this.curIndex = 0    
    this.state = {selectedIndex: 0};
    this.numItems = THUMB_URIS.length;
    this.itemWidth = (FIXED_BAR_WIDTH / this.numItems) - ((this.numItems - 1) * BAR_SPACE);

    this.onTVKey = this.onTVKey.bind(this);    
    this._handleButtonPressRight = this._handleButtonPressRight.bind(this);
    this._handleButtonPressLeft = this._handleButtonPressLeft.bind(this);    
  }

  _handleButtonPressRight = () => {
   // JuvoPlayer.log("curIndex = " + this.curIndex);
   // JuvoPlayer.log("numItems = " + this.numItems);
    this.scroll_inprogress = false;    
    if (this.curIndex < this.numItems - 1) {      
      this.curIndex++;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });
    }    
    this.setState({selectedIndex: this.curIndex });
  };

  _handleButtonPressLeft = () => {
   // JuvoPlayer.log("curIndex = " + this.curIndex);
   // JuvoPlayer.log("numItems = " + this.numItems);
    this.scroll_inprogress = false;
    if (this.curIndex > 0) {     
      this.curIndex--;
      this._scrollView.scrollTo({ x: this.curIndex * deviceWidth, y: 0, animated: true });  
    };
    this.setState({selectedIndex: this.curIndex});
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
    JuvoPlayer.log("ScrollView clicked...");
    switch (pressed.KeyName)
      {
        case "Right":
            JuvoPlayer.log("Right clicked...");
            this._handleButtonPressRight();
            break;
        case "Left":
          JuvoPlayer.log("Left clicked...");
          this._handleButtonPressLeft();
            break;        
      }  
    JuvoPlayer.log("hello from Tizen world! params - KeyName  " + pressed.KeyName + " the code: " + pressed.KeyCode);
  };  
   
  render() {    
    const index = this.state.selectedIndex;
    const renderThumbs = (uri, i) => <Thumb key={i} source={uri} myIndex={i} selectedIndex={index} />;
    return (
      <View style={styles.horizontalScrollView}>
        <ScrollView          
          scrollEnabled = {false}
          ref={(scrollView) => {this._scrollView = scrollView;}}                  
          automaticallyAdjustContentInsets={false}
          scrollEventThrottle={16}
          horizontal={true}
          showsHorizontalScrollIndicator={false}
          style={[styles.scrollView]}>
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
      visible: true
    };
    this.toggle = this.toggle.bind(this);
    this.onTVKey = this.onTVKey.bind(this);
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

  onTVKey(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode   

    switch (pressed.KeyName)
      {
        case "Return":
        case "XF86AudioPlay":     
            JuvoPlayer.log("Start playback...");
            if (this.state.visible) {
              //JuvoPlayer.startPlayback();
              this.toggle();
            }
            else {
              //pause
              //JuvoPlayer.pauseResumePlayback();
            }
            break;
        case "XF86Back":
        case "XF86AudioStop":
            if (!this.state.visible) {            
              //JuvoPlayer.stopPlayback();
              this.toggle();
            }  
            break;        
      }  
    JuvoPlayer.log("hello from Tizen world! params - KeyName  " + pressed.KeyName + " the code: " + pressed.KeyCode);
  }

  render() {
    return (
      <View style={styles.container}>
        <HideableView
          visible={this.state.visible}
          style={styles.clip_details}>
            <Image style={styles.img_big} source={require('./res/images/car.png')} />
            <Text style={styles.clip_details_text} >
              {THUMB_URIS[0]}
            </Text>
          <HorizontalScrollView/>
        </HideableView >               
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
    return this.props.visible !== nextProps.visible;
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
      <Animated.View style={{ opacity: this.state.opacity }}>
        {this.props.children}
      </Animated.View>
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
    flex: 1,
    flexDirection: 'column',
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'transparent',
    width: 1920,
    height: 1080,
    top: 0,
    left: 0, 
    position: 'absolute'
  },
  scrollView: {
    top: -265,
    left: 0, 
    position: 'absolute',    
    height: 300,
    width: 1920,
    backgroundColor: "#adff2f"
  },
  horizontalScrollView: {
    flex: 1,
    height: '100%',
    width: 1920,
    flexDirection: 'column',
    justifyContent: 'flex-end',
    alignItems: 'flex-end',
  },
  thumb: {
    flex: 1,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#cccccc',
    width: 459,
    height: 260
  },
  thumb_selected : {    
    position: 'relative',
    top: 0,
    left: 0,
    alignItems: 'center', 
    justifyContent: 'center',   
    backgroundColor: '#ffd700',
    width: 459,
    height: 260
  },
  img_thumb: {
    justifyContent: 'center',
    alignItems: 'center',
    width: '98%',
    height: '98%',
  },
  img_big: {    
    position: "absolute",
    top: 0,
    left: 0,
    width: 1920,
    height: 1080
  },   
  clip_details: {
    position: "absolute",
    top: 0,
    left: 0,
    width: 1920,
    height: 1080
  },
  clip_details_text: {    
    position: "relative",
    top: 500,
    left: 150,
    width: '100%',
    height: '100%',
    fontSize: 30,
    color: '#000000'
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

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
//import console = require('console');
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
  shouldComponentUpdate(nextProps, nextState) {
    return false;
  }

  render() {
    return (
      <View style={styles.thumb}>
        <Button style={styles.button_thumb}
          title=''
          onPress={() => PlayVideo(this.props.source)}
          name={this.props.source}
        >
        </Button>
        <Image style={styles.img} source={{ uri: this.props.source }} />
      </View>
    );
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
var createThumbRow = (uri, i) => <Thumb key={i} source={uri} />;

class HorizontalScrollView extends React.Component {
  render() {
    var _scrollView;
    var curIndex = 0
    var numItems = THUMB_URIS.length
    var itemWidth = (FIXED_BAR_WIDTH / numItems) - ((numItems - 1) * BAR_SPACE)
    var animVal = new Animated.Value(0)

    var _handleButtonPressRight = () => {
      if (curIndex < numItems - 1) {
        curIndex++;
        _scrollView.scrollTo({ x: curIndex * deviceWidth, y: 0, animated: true })
      }
    };
    var _handleButtonPressLeft = () => {
      if (curIndex > 0) {
        curIndex--;
        _scrollView.scrollTo({ x: curIndex * deviceWidth, y: 0, animated: true })
      }
    };

    return (
      <View style={styles.horizontalScrollView}>
        <ScrollView
          ref={(scrollView) => { _scrollView = scrollView; }}
          automaticallyAdjustContentInsets={false}
          scrollEventThrottle={100}
          horizontal={true}
          showsHorizontalScrollIndicator={false}
          style={[styles.scrollView]}>
          {THUMB_URIS.map(createThumbRow)}
        </ScrollView>
        <View style={styles.control_buttons}>
          <Button
            title="Scroll to left"
            accessibilityLabel="Learn more abo ut this  button"
            onPress={_handleButtonPressLeft}>
          </Button>
          <Button
            title="Scroll to start"
            accessibilityLabel="Learn more about this  button"
            onPress={() => { _scrollView.scrollTo({ x: 0 }); }}>
          </Button>
          <Button
            title="Scroll to right"
            accessibilityLabel="Learn more about this  button"
            onPress={_handleButtonPressRight}>
          </Button>
        </View>
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
              JuvoPlayer.startPlayback();
              this.toggle();
            }
            else {
              //pause
            }
            break;
        case "XF86Back":
        case "XF86AudioStop":
            if (!this.state.visible) {            
              JuvoPlayer.stopPlayback();
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
          <Text style={styles.clip_details_text}>
            Hello world!
          </Text>
        </HideableView >
        
        <HideableView
          visible={this.state.visible}>

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
    justifyContent: 'flex-end',
    alignItems: 'flex-end',
    backgroundColor: "transparent",
    width: '100%',
    height: '100%'
  },
  scrollView: {
    height: '46%',
    width: 1920,
  },
  horizontalScrollView: {
    flexDirection: 'column',
    justifyContent: 'flex-end',
    alignItems: 'flex-end',
  },
  text: {
    fontSize: 16,
    fontWeight: 'bold',
    margin: 5,
  },
  button_thumb: {
    position: 'absolute',
    alignItems: 'center',
    backgroundColor: '#00000070',
    width: '100%',
    height: '100%'
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
  img: {
    justifyContent: 'center',
    alignItems: 'center',
    width: '96%',
    height: '96%',
  },
  img_big: {
    width: '100%',
    height: '100%',
    position: "absolute",
    top: 0,
    left: 0
  },
  img_big_hide: {
    position: 'absolute',
    width: '10%',
    height: '10%',
    backgroundColor: "transparent",
    top: 0,
    left: 0
  },
  control_buttons: {
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    flexDirection: 'row',
    alignItems: 'stretch'
  },
  clip_details: {
    position: "absolute",
    top: 0,
    left: 0,
    width: '100%',
    height: '100%'
  },
  clip_details_text: {
    width: 1920,
    height: 1080,
    backgroundColor: '#345636',
    fontSize: 50,
    textAlign: 'right',
    textAlignVertical: 'center'
    
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

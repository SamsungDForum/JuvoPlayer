/**
 * Sample React Native App
 * https://github.com/facebook/react-native
 * @flow
 */
'use strict'

import React, { Component } from 'react';
import {
  ScrollView,
  Image,
  View,
  TouchableOpacity,
  Text,
  StyleSheet,
  AppRegistry,
  Alert,
  Button
} from 'react-native';

//var NativeEventEmitter = require('NativeEventEmitter');
import { NativeModules } from 'react-native';
const JuvoPlayer = NativeModules.JuvoPlayer;
//var JuvoPlayerEventEmitter = new NativeEventEmitter(JuvoPlayer);
var ProgressBar = require('ProgressBarAndroid');
var TimerMixin = require('react-timer-mixin');

class Thumb extends React.Component {

  shouldComponentUpdate(nextProps, nextState) {
    return false;
  }

  render() {
    return (
        <View style={styles.thumb}>
            <Image style={styles.img} source={{uri: this.props.source}} />
        </View>
    );
  }

}

class VerticalScrollView extends React.Component {
  //title: '<ScrollView>',
  //description: 'To make content scrollable, wrap it within a <ScrollView> component',
  render () {
    var _scrollView;
    return (
      <View>
        <ScrollView
          ref={(scrollView) => { _scrollView = scrollView; }}
          automaticallyAdjustContentInsets={false}
          onScroll={() => { console.log('onScroll!'); }}
          scrollEventThrottle={200}
          style={styles.scrollView}>
          {THUMB_URLS.map(createThumbRow)}
        </ScrollView>
        <TouchableOpacity
          style={styles.button}
          onPress={() => { _scrollView.scrollTo({y: 0}); }}>
          <Text style = {{color:'black'}}>Scroll to top</Text>
        </TouchableOpacity>
          <TouchableOpacity
          style={styles.button}
          onPress={() => { _scrollView.scrollToEnd({animated: true}); }}>
          <Text style = {{color:'black'}}>Scroll to bottom</Text>
        </TouchableOpacity>
      </View>
    );
  }
}

class HorizontalScrollView extends React.Component {
  //title: '<ScrollView> (horizontal = true)',
  //description: 'You can display <ScrollView>\'s child components horizontally rather than vertically',
  render() {
    var _scrollView;
    var addtionalStyles = {direction: 'ltr'};
    //{direction: 'rtl'}
    var title = 'HorizontalScroll';
    return (
        <View style={addtionalStyles}>
          <Text style={styles.text}>{title}</Text>
          <ScrollView
            ref={(scrollView) => { _scrollView = scrollView; }}
            automaticallyAdjustContentInsets={false}
            horizontal={true}
            style={[styles.scrollView, styles.horizontalScrollView]}>
            {THUMB_URI.map(createThumbRow)}
          </ScrollView>
          <TouchableOpacity
            style={styles.button}
            onPress={() => { _scrollView.scrollTo({x: 0}); }}>
            <Text style = {{color:'black'}}>Scroll to start</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={styles.button}
            onPress={() => { _scrollView.scrollToEnd({animated: true}); }}>
            <Text style = {{color:'black'}}>Scroll to end</Text>
          </TouchableOpacity>
        </View>
      );
  }
}

var THUMB_URLS = [
  require('./Thumbnails/like.png'),
  require('./Thumbnails/dislike.png'),
  require('./Thumbnails/call.png') 
];
var THUMB_URI = [
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true',
  'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true'  
];
//THUMB_URLS = THUMB_URLS.concat(THUMB_URLS); // double length of THUMB_URLS

var createThumbRow = (uri, i) => <Thumb key={i} source={uri} />;

var MovingBar = React.createClass({
  mixins: [TimerMixin],

  getInitialState: function() {
    return {
      progress: 0
    };
  },

  componentDidMount: function() {
    this.setInterval(
      () => {
        var progress = (this.state.progress + 0.02) % 1;
        this.setState({progress: progress});
      }, 50
    );
  },

  render: function() {
    return <ProgressBar progress={this.state.progress} {...this.props} />;
  },
});

const onButtonPress = () => {
  try {
    JuvoPlayer.startPlayback();    
  } catch (e) {
    Alert.alert('Error! ' + e);
  }
};

export default class JuvoReactNative extends Component {  
  render() {
    return (
      <View style={{width:1920, height: 1080}}>
        <Button style={{width:300, height: 100}}
                onPress={onButtonPress}
                title="Start!"
                accessibilityLabel="See an informative alert"
        />
        <Image source={{uri: 'https://github.com/SamsungDForum/JuvoPlayer/blob/master/smarthubpreview/pictures/car.jpg?raw=true', width: 208, height: 180}} />
        <Image style={styles.img} source={'./Tizen/res/tiles/bolid.jpg'} />
        <MovingBar horizontal={true} style={{top:750, width:1860, height:40, backgroundColor: 'green', color: 'blue'}} />
        <HorizontalScrollView />
      </View>
    );
  }
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',    
    backgroundColor: "transparent",
  },
  welcome: {
    fontSize: 20,
    textAlign: 'center',
    margin: 10,
  },
  instructions: {
    textAlign: 'center',
    color: '#333333',
    marginBottom: 5,
  },
  scrollView: {
    backgroundColor: '#eeeeee',
    height: 350,
  },
  horizontalScrollView: {
    height: 406,    
    width: 1650
  },
  text: {
    fontSize: 16,
    fontWeight: 'bold',
    margin: 5,
  },
  button: {
    margin: 5,
    padding: 5,
    alignItems: 'center',
    backgroundColor: '#cccccc',
    borderRadius: 3,
  },
  thumb: {
    margin: 5,
    padding: 5,
    backgroundColor: '#cccccc',
    borderRadius: 3,
    minWidth: 96,    
  },
  img: {
    width: 464,
    height: 264,
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);

'use strict'
import React, { Component } from 'react';
import { 
  View,  
  NativeModules,
  NativeEventEmitter
} from 'react-native';

import HideableView from './HideableView';
import ContentPicture from './ContentPicture';
import ContentScrollView from './ContentScrollView';
import LocalResources from '../LocalResources';

export default class ContentCatalog extends Component {
  
  constructor(props) {
    super(props);
    this.state = {
      visible: true,
      selectedClipIndex: 0,      
      bigPictureVisible: true
    };
    this.toggle = this.toggle.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onTVKeyUp = this.onTVKeyUp.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);   
    this.onBigPictureLoadStart = this.onBigPictureLoadStart.bind(this);
    this.onBigPictureLoadEnd = this.onBigPictureLoadEnd.bind(this);
    this.bigPictureFadeDuration = 500;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  toggle() {
    this.setState({
      visible: !this.state.visible
    });
  }

  componentWillMount() {
    this.JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );
    this.JuvoEventEmitter.addListener(
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
          // this.JuvoPlayer.startPlayback();
          this.toggle();
        }
        else {
          //pause
          // this.JuvoPlayer.pauseResumePlayback();
        }
        break;
      case "XF86Back":
      case "XF86AudioStop":
        if (!this.state.visible) {
         //  this.JuvoPlayer.stopPlayback();
          this.toggle();
        }
        break;
    }   
  }

  onTVKeyUp(pressed) {         
      this.setState({bigPictureVisible: true}); 
  }

  handleSelectedIndexChange(index) {     
    this.props.onSelectedIndexChange(index);
    this.setState({selectedClipIndex: index});      
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  } 

  onBigPictureLoadStart(event) { 
  }

  onBigPictureLoadEnd(event) {  
  }

  render() {    
    const index = this.state.selectedClipIndex; 
    const visible = (index == 1);
    const uri = LocalResources.tileNames[index];
    const path = LocalResources.tilePathSelect(uri);
    const styles = this.props.styles.container;
    return (
      <View style={styles}>
        <HideableView  visible={this.state.visible} duration={300}>
          <HideableView  visible={this.state.bigPictureVisible} duration={300} style={{zIndex: -100 }}>          
            <View style={{position: 'relative', top: 0, width: 1920, height: 1080, zIndex: -10 }}>
              <ContentPicture key={index} source={uri} myIndex={index} selectedIndex={index}
                      path={path}
                      width={1920} height={1080} top={0} left = {0} position={'relative'} visible={true} fadeDuration={this.bigPictureFadeDuration} 
                      stylesThumbSelected={{width: 1920, height: 1080}} stylesThumb={{width: 1920, height: 1080}}
                      onLoadStart = {this.onBigPictureLoadStart} onLoadEnd = {this.onBigPictureLoadEnd}/>

            </View>   
          </HideableView>   
          <View style={{position: 'relative', top: -1080, width: 1920, height: 1080, zIndex: 100 }}>
            <ContentScrollView stylesThumbSelected={styles.stylesThumbSelected} stylesThumb={styles.stylesThumb} 
                              onSelectedIndexChange={this.handleSelectedIndexChange} contentURIs={LocalResources.tileNames}  />
          </View>   
        </HideableView> 
      </View>
    );
  }
}
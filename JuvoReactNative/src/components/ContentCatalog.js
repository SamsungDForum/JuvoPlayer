'use strict'
import React, { Component } from 'react';
import { 
  View,  
  NativeModules,
  NativeEventEmitter
} from 'react-native';

import HideableView from './HideableView';
import ContentPicture from './ContentPicture';
import ContentScroll from './ContentScroll';
import ResourceLoader from '../ResourceLoader';
import InProgressView from './InProgressView';

export default class ContentCatalog extends Component {  
  constructor(props) {
    super(props);
    this.state = {   
      selectedClipIndex : 0
    };    
    this.visible = this.props.visibility; 
    this.bigPictureVisible = this.visible;    
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
    this.JuvoEventEmitter.addListener(
      'onTVKeyDown',
      this.onTVKeyDown
    );
    this.JuvoEventEmitter.addListener(
      'onTVKeyUp',
      this.onTVKeyUp
    );   
  }
  componentDidUpdate(prevProps, prevState) {   
    this.bigPictureVisible = true;    
  }
  shouldComponentUpdate(nextProps, nextState) {  
    return true;
  } 
  toggleVisibility() {  
   this.visible = !this.visible; 
   this.props.switchView('PlaybackView', !this.visible);  
  }
  rerender() {
    this.setState({selectedIndex: this.state.selectedIndex});
  }  
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode         
    if (this.keysListenningOff) return;  
    switch (pressed.KeyName) {     
      case "XF86Back":
      case "XF86AudioStop":         
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":                 
        this.toggleVisibility();
        break;
      case "Left", "Right":
                    
        break;            
    }      
    if (this.bigPictureVisible) {      
      //hide big picture during the fast scrolling (long key press)
      this.bigPictureVisible = false;  
      this.rerender();
    }       
  }
  onTVKeyUp(pressed) {   
    if (this.keysListenningOff) return;
    this.bigPictureVisible = true;    
    this.rerender();
  } 
  handleSelectedIndexChange(index) { 
    this.props.onSelectedIndexChange(index);  
    this.setState({selectedClipIndex: index});    
  }  
  handleBigPicLoadStart() {       
  }
  handleBigPicLoadEnd() {    
    this.bigPictureVisible = true;   
  }
  render() {    
    const index = this.state.selectedClipIndex ? this.state.selectedClipIndex : 0;    
    const uri = ResourceLoader.tileNames[index];
    const path = ResourceLoader.tilePathSelect(uri);   
    const overlay = ResourceLoader.tilesPath.contentDescriptionBackground;    
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    this.keysListenningOff = !visibility; 
    const showBigPicture = this.bigPictureVisible; 
    return (
      <View >
        <HideableView  visible={visibility} duration={300}>
          <HideableView  visible={showBigPicture} duration={100} style={{zIndex: -20 }}>          
            <View style={{top: 0, left: 650, width: 1270, height: 800, zIndex: -11  }}>
              <ContentPicture source={uri} selectedIndex={index} 
                      path={path} onLoadEnd={this.handleBigPicLoadEnd} onLoadStart={this.handleBigPicLoadStart}
                      width={1266} height={715} top={0} left = {0}                         
                      />              
            </View>              
          </HideableView> 
          <View style={{ top: -800, left: 650, width: 1270, height: 800, zIndex: -10 }}>
            <ContentPicture  source={uri} selectedIndex={index} 
                      path={overlay}
                      width={1266} height={715} top={0} left = {0}                         
                      />
            </View>  
          <View style={{top: -1600, width: 1920, height: 1080, zIndex: 100 }}>
            <ContentScroll onSelectedIndexChange={this.handleSelectedIndexChange}
                           contentURIs={ResourceLoader.tileNames} 
                           keysListenningOff={this.keysListenningOff}/>
          </View>                      
        </HideableView> 
      </View>
    );
  }
}
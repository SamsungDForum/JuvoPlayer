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
      bigPictureVisible: true,
      selectedClipIndex : 0
    };    
    this.visible = this.props.visibility;  
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

  toggleVisibility() {    
   this.visible = !this.visible; 
   this.props.switchVisibility('PlaybackView', !this.visible);  
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
    if (this.keysListenningOff) return;    
  
    switch (pressed.KeyName) {     
      case "XF86Back":
      case "XF86AudioStop":         
      case "Return":
      case "XF86AudioPlay":
      case "XF86PlayBack":         
        this.toggleVisibility();              
        break;
    }   

    this.setState({bigPictureVisible: false});
  }
  onTVKeyUp(pressed) {        
    if (this.keysListenningOff) return;
    this.setState({bigPictureVisible: true});      
  }  

  handleSelectedIndexChange(index) {     
    this.props.onSelectedIndexChange(index);  
    this.setState({selectedClipIndex: index});
  }

  shouldComponentUpdate(nextProps, nextState) {       
    return true;
  } 

  handleBigPicLoadStart() {   
    this.setState({bigPictureVisible: false});
  }

  handleBigPicLoadEnd() {  
    this.setState({bigPictureVisible: true });
  }

  render() {    
    const index = this.state.selectedClipIndex;    
    const uri = LocalResources.tileNames[index];
    const path = LocalResources.tilePathSelect(uri);   
    const overlay = LocalResources.tilesPath.contentDescriptionBackground;
    const fadeduration = 200;
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    this.keysListenningOff = !visibility; 
    return (
      <View style={{backgroundColor: 'transparent'}}>
        <HideableView  visible={visibility} duration={fadeduration}>
          <HideableView  visible={this.state.bigPictureVisible} duration={fadeduration} style={{zIndex: -100 }}>          
            <View style={{position: 'relative', top: 0, left: 650, width: 1270, height: 800, zIndex: -11  }}>
              <ContentPicture source={uri} selectedIndex={index} 
                      path={path} onLoadEnd={this.handleBigPicLoadEnd} onLoadStart={this.handleBigPicLoadStart}
                      width={1266} height={715} top={0} left = {0} position={'relative'}                        
                      />              
            </View>              
          </HideableView> 
          <View style={{position: 'relative', top: -800, left: 650, width: 1270, height: 800, zIndex: -10 }}>
            <ContentPicture  source={uri} selectedIndex={index} 
                      path={overlay}
                      width={1266} height={715} top={0} left = {0} position={'relative'}                        
                      />
            </View>  
          <View style={{position: 'relative', top: -1600, width: 1920, height: 1080, zIndex: 100 }}>
            <ContentScrollView onSelectedIndexChange={this.handleSelectedIndexChange} contentURIs={LocalResources.tileNames}  keysListenningOff={this.keysListenningOff}/>
          </View>   
        </HideableView> 
      </View>
    );
  }
}
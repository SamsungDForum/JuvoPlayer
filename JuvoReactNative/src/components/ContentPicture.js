
'use strict'
import React from 'react';
import {  
  View,  
  Image  
} from 'react-native';

import HideableView from './HideableView';
import LocalResources from '../LocalResources';

export default class ContentPicture extends React.Component {
    
    constructor(props) {
      super(props); 
    }
  
    shouldComponentUpdate(nextProps, nextState) {    
       return true;
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
      const path = this.props.path ? this.props.path : LocalResources.tilesPath.default;
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
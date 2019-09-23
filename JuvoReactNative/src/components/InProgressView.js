'use strict'
import React from 'react';
import {  
  View,
  Image,
  NativeModules,
  NativeEventEmitter,  
  Text
} from 'react-native';

import ResourceLoader from '../ResourceLoader';
import HideableView from './HideableView';

export default class InProgressView extends React.Component {
  constructor(props) {
    super(props);  
    this.visible =  this.props.visibility ? this.props.visibility : false;  
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
  }

  render() {     
    const visibility = this.props.visibility ? this.props.visibility : this.visible;   
    this.visible = visibility;
    const fadeduration = 300; 
    this.JuvoPlayer.log("InProgressView  visibility = " + visibility);
    return (
      <View>  
          <HideableView visible={visibility} duration={fadeduration}>   
                <ProgressCircle style={{top: -500, left: 800, backgroundColor: '#000000', opacity: 0.8}}
                    value={100}
                    size={320}
                    thickness={20}
                    color="#2b80ff"
                    unfilledColor="#f2f2f2"
                    animationMethod="timing"
                    animationConfig={{ speed: 4, useNativeDriver: true }}>
                    <Text style={{ color: '#2b80ff', fontSize: 18, fontWeight: 'bold' }}>
                        {`${Math.floor(0.3 * 100)}%`}
                    </Text>
                </ProgressCircle>
          </HideableView>
      </View>
    );
  }
}
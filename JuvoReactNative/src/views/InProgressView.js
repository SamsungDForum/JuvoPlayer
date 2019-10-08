'use strict'
import React from 'react';
import {  
  View,  
  Text,
  ActivityIndicator
} from 'react-native';

import HideableView from './HideableView';

export default class InProgressView extends React.Component {
  constructor(props) {
    super(props); 
  }    
  render() {         
    const fadeduration = 300;   
    return (
      <View>  
          <HideableView visible={this.props.visible} duration={fadeduration}> 
          <View style={{width: 200, height: 200, justifyContent: 'center', alignItems: 'center', backgroundColor: '#000000', opacity: 0.6}}>
            <ActivityIndicator style={{left: 0, top: -10}} size="large" color="#00ff00" />
            <Text style={{left: 0, top: 10, color: '#00ff00', fontSize: 18, fontWeight: 'bold'}}>
                {this.props.message}
            </Text>
          </View>
          </HideableView>
      </View>
    );
  }
}
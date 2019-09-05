'use strict'
import React, { Component, PropTypes } from 'react';
import {  
  View,  
  Animated  
} from 'react-native'

export default class DisappearingView extends Component {
   
    constructor(props) {
      super(props);
      this.state = {
        opacity: new Animated.Value(this.props.visible ? 1 : 0)
      }
    }
  
    animate(show) {
      const duration = this.props.duration ? parseInt(this.props.duration) : 500;
      const timeOnScreen = this.props.timeOnScreen ? parseInt(this.props.timeOnScreen) : 3000; //3 sec is default on screen time
    
      //Animated.EndCallback = this.props.onDisappeared();
      Animated.sequence([
        // decay, then spring to start and twirl
        Animated.timing(
             this.state.opacity, {
               toValue: show ? 1 : 0,
               duration: !this.props.noAnimation ? duration : 0
             }
           ),
        Animated.delay(timeOnScreen),
        Animated.timing(
          this.state.opacity, {
            toValue: 0,
            duration: !this.props.noAnimation ? duration : 0
          }
        )
        ]).start(this.props.onDisappeared); // start the sequence group        
    }

  //() => {this.props.onDisappeared()}
    shouldComponentUpdate(nextProps) {   
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
  
  DisappearingView.propTypes = {
    visible: PropTypes.bool.isRequired,
    timeOnScreen: PropTypes.number,
    duration: PropTypes.number,
    removeWhenHidden: PropTypes.bool,
    noAnimation: PropTypes.bool
  }
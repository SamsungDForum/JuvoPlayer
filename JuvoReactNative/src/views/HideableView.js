'use strict';
import React, { Component, PropTypes } from 'react';
import { View, Animated } from 'react-native';

export default class HideableView extends Component {
  constructor(props) {
    super(props);
    this.state = {
      opacity: new Animated.Value(this.props.visible ? 1 : 0)
    };
  }

  animate(show) {
    const duration = this.props.duration ? parseInt(this.props.duration) : 500;
    Animated.timing(this.state.opacity, {
      toValue: show ? 1 : 0,
      duration: !this.props.noAnimation ? duration : 0
    }).start();
  }

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
      return this.visible && this.props.children;
    }

    const width = this.props.width === undefined ? '100%' : this.props.width;
    const height = this.props.height === undefined ? '100%' : this.props.height;

    return (
      <View style={{ position: this.props.position, width: width, height: height }}>
        <Animated.View style={{ opacity: this.state.opacity }}>{this.props.children}</Animated.View>
      </View>
    );
  }
}

HideableView.propTypes = {
  visible: PropTypes.bool.isRequired,
  duration: PropTypes.number,
  removeWhenHidden: PropTypes.bool,
  noAnimation: PropTypes.bool
};

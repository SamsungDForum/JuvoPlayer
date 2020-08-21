'use strict';
import React, { Component } from 'react';
import { View, Text } from 'react-native';

export default class ContentDescription extends Component {
  constructor(props) {
    super(props);
  }

  shouldComponentUpdate(nextProps, nextState) {
    return true;
  }
  render() {
    this.handleSelectedIndexChange = this.props.onSelectedIndexChange;
    return (
      <View style={this.props.viewStyle}>
        <Text style={this.props.headerStyle}>{this.props.headerText}</Text>
        <Text style={this.props.bodyStyle}>{this.props.bodyText}</Text>
      </View>
    );
  }
}

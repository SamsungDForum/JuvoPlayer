'use strict';
import React from 'react';
import { View, Text, Picker, NativeModules, NativeEventEmitter, StyleSheet, DeviceEventEmitter } from 'react-native';

import HideableView from './HideableView';
import Native from '../Native';

export default class PlaybackSettingsView extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      selectedIndex: -1
    };
    this.uniqueKey = 0;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.keysListenningOff = false;
    this.handleConfirmSettings = this.handleConfirmSettings.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.pickerChange = this.pickerChange.bind(this);
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('PlaybackSettingsView/onTVKeyDown', this.onTVKeyDown);
  }

  componentWillUnmount() {
    DeviceEventEmitter.removeListener('PlaybackSettingsView/onTVKeyDown', this.onTVKeyDown);
  }

  handleConfirmSettings() {
    this.props.onCloseSettingsView();
  }

  componentWillReceiveProps(nextProps) {
    if (this.state.selectedIndex !== nextProps.streamsData.selectedIndex) {
      this.uniqueKey = this.uniqueKey + 1;
      this.setState({selectedIndex: nextProps.streamsData.selectedIndex});
    }
  }

  getDefaultStreamDescription(streams) {
    let defaultStream = this.getDefaultStream(streams);
    if (defaultStream !== undefined) return defaultStream.Description;
    return '';
  }

  getDefaultStream(streams) {
    for (let stream of streams) {
      if (stream.Default === true) {
        return stream;
      }
    }
  }

  onTVKeyDown(pressed) {
    switch (pressed.KeyName) {
      case 'XF86Back':
      case 'XF86AudioStop':
        this.handleConfirmSettings();
        break;
    }
  }

  pickerChange(itemIndex, settingName) {
    //Apply the playback setttings to the playback
    this.JuvoPlayer.SetStream(itemIndex, Native.JuvoPlayer.Common.StreamType[settingName]);
  }

  render() {
    const fadeduration = 300;
    return (
      <View style={{ width: 1600, height: 350 }} key={this.uniqueKey}>
        <HideableView visible={this.props.visible} duration={fadeduration}>
          <View style={styles.transparentPage}>
            <View style={[styles.textView, { flex: 1.5 }]}>
              <Text style={styles.textHeader}> Use arrow keys to navigate. Press enter key to select a setting. </Text>
            </View>
            <View style={{ flex: 2, alignItems: 'flex-start', flexDirection: 'row', backgroundColor: 'transparent' }}>
              <View style={{ flex: 1, alignItems: 'center' }}>
                <View>
                  <Text style={styles.textBody}>Audio track</Text>
                  <Picker
                    title={this.getDefaultStreamDescription(this.props.streamsData.Audio)}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Audio');
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Audio.map((item, index) => {
                      return <Picker.Item label={item.Description} value={item.Id} key={index} />;
                    })}
                  </Picker>
                </View>
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                <View>
                  <Text style={styles.textBody}>Video quality</Text>
                  <Picker
                    title={this.getDefaultStreamDescription(this.props.streamsData.Video)}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Video');
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Video.map((item, index) => {
                      return <Picker.Item label={item.Description} value={item.Id} key={index} />;
                    })}
                  </Picker>
                </View>
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                <View>
                  <Text style={styles.textBody}>Subtitles</Text>
                  <Picker
                    title={this.getDefaultStreamDescription(this.props.streamsData.Subtitle)}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Subtitle');
                      this.props.onSubtitleSelection(this.props.streamsData.Subtitle[itemIndex].Description);
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Subtitle.map((item, index) => {
                      return <Picker.Item label={item.Description} value={item.Id} key={index} />;
                    })}
                  </Picker>
                </View>
              </View>
            </View>
            <View style={[styles.textView, { flex: 1 }]}>
              <Text style={styles.textFooter}> Press return key to close </Text>
            </View>
          </View>
        </HideableView>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  picker: {
    height: 30,
    width: 450,
    color: '#ffffff'
  },
  textView: {
    justifyContent: 'center',
    backgroundColor: 'transparent',
    opacity: 1
  },
  transparentPage: {
    width: '100%',
    height: '100%',
    backgroundColor: 'black',
    opacity: 0.8
  },
  textHeader: {
    fontSize: 30,
    color: 'white',
    alignSelf: 'center'
  },
  textFooter: {
    fontSize: 20,
    color: 'white',
    textAlign: 'center'
  },
  textBody: {
    fontSize: 28,
    color: 'white',
    fontWeight: 'bold'
  }
});

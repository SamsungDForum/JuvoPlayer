'use strict';
import React from 'react';
import { View, Text, Picker, NativeModules, NativeEventEmitter, StyleSheet } from 'react-native';

import HideableView from './HideableView';
import Native from '../Native';

export default class PlaybackSettingsView extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      streamsData: -1
    };
    this.settings = {
      audioSetting: -1,
      videoSetting: -1,
      subtitleSetting: -1
    };
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.keysListenningOff = false;
    this.handleConfirmSettings = this.handleConfirmSettings.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.pickerChange = this.pickerChange.bind(this);
  }
  componentWillMount() {
    this.JuvoEventEmitter.addListener('onTVKeyDown', this.onTVKeyDown);
  }
  handleConfirmSettings() {
    this.keysListenningOff = true;
    this.props.onCloseSettingsView(this.settings);
  }
  componentWillReceiveProps(nextProps) {
    const result = this.state.streamsData.selectedIndex !== nextProps.streamsData.selectedIndex;
    if (result) {
      this.settings = {
        audioSetting: -1,
        videoSetting: -1,
        subtitleSetting: -1
      };
      this.setState({
        streamsData: nextProps.streamsData
      });
    }
    this.keysListenningOff = false;
  }
  onTVKeyDown(pressed) {
    if (this.keysListenningOff) return;
    switch (pressed.KeyName) {
      case 'XF86Back':
      case 'XF86AudioStop':
        this.handleConfirmSettings();
        break;
    }
  }
  pickerChange(itemIndex, settingName) {
    //Apply the playback setttings to the playback
    this.state.streamsData[settingName].map((v, i) => {
      if (itemIndex === i) {
        switch (settingName) {
          case 'Audio':
            this.settings.audioSetting = this.state.streamsData.Audio[itemIndex].Id;
            break;
          case 'Video':
            this.settings.videoSetting = this.state.streamsData.Video[itemIndex].Id;
            break;
          case 'Subtitle':
            this.settings.subtitleSetting = this.state.streamsData.Subtitle[itemIndex].Id;
            break;
        }
      }
    });
    this.JuvoPlayer.SetStream(itemIndex, Native.JuvoPlayer.Common.StreamType[settingName]);
  }
  render() {
    const fadeduration = 300;
    return (
      <View style={{ width: 1600, height: 350 }}>
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
                    selectedValue={this.settings.audioSetting}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Audio');
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Audio.map((item, index) => {
                      if (item.Default === true && this.settings.audioSetting === -1) {
                        this.settings.audioSetting = item.Id;
                      }
                      return <Picker.Item label={item.Description} value={item.Id} key={index} />;
                    })}
                  </Picker>
                </View>
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                <View>
                  <Text style={styles.textBody}>Video quality</Text>
                  <Picker
                    selectedValue={this.settings.videoSetting}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Video');
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Video.map((item, index) => {
                      if (item.Default === true && this.settings.videoSetting === -1) {
                        this.settings.videoSetting = item.Id;
                      }
                      return <Picker.Item label={item.Description} value={item.Id} key={index} />;
                    })}
                  </Picker>
                </View>
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                <View>
                  <Text style={styles.textBody}>Subtitles</Text>
                  <Picker
                    selectedValue={this.settings.subtitleSetting}
                    style={styles.picker}
                    onValueChange={(itemValue, itemIndex) => {
                      this.JuvoPlayer.Log('itemValue = ' + itemValue);
                      this.pickerChange(itemIndex, 'Subtitle');
                      this.props.onSubtitleSelection(this.state.streamsData.Subtitle[itemIndex].Description);
                    }}
                    enabled={this.props.visible}>
                    {this.props.streamsData.Subtitle.map((item, index) => {
                      if (item.Default === true && this.settings.subtitleSetting === -1) {
                        this.settings.subtitleSetting = item.Id;
                      }
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

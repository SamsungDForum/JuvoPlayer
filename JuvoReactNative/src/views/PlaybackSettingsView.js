"use strict";
import React from "react";
import { View, Text, Picker, NativeModules, NativeEventEmitter } from "react-native";

import HideableView from "./HideableView";
import Native from "../Native";

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
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
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
      case "XF86Back":
      case "XF86AudioStop":
        this.handleConfirmSettings();
        break;
    }
  }
  pickerChange(itemIndex, settingName) {
    //Apply the playback setttings to the playback
    this.state.streamsData[settingName].map((v, i) => {
      if (itemIndex === i) {
        switch (settingName) {
          case "Audio":
            this.settings.audioSetting = this.state.streamsData.Audio[itemIndex].Id;
            break;
          case "Video":
            this.settings.videoSetting = this.state.streamsData.Video[itemIndex].Id;
            break;
          case "Subtitle":
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
          <View style={{ width: '100%', height: '100%', justifyContent: "center", alignItems: "center", backgroundColor: "#000000", opacity: 0.8 }}>
            <Picker
              selectedValue={this.settings.audioSetting}
              style={{ left: -500, top: 100, height: 30, width: 450, color: "#ffffff" }}
              onValueChange={(itemValue, itemIndex) => {
                this.JuvoPlayer.Log("itemValue = " + itemValue);
                this.pickerChange(itemIndex, "Audio");
              }}
              enabled={this.props.visible}>
              {this.props.streamsData.Audio.map((item, index) => {
                if (item.Default === true && this.settings.audioSetting === -1) {
                  this.settings.audioSetting = item.Id;
                }
                return <Picker.Item label={item.Description} value={item.Id} key={index} />;
              })}
            </Picker>
            <Text style={{ left: -645, top: 30, color: "#ffffff", fontSize: 28, fontWeight: "bold" }}>Audio track</Text>
            <Picker
              selectedValue={this.settings.videoSetting}
              style={{ left: 0, top: 33, height: 30, width: 450, color: "#ffffff" }}
              onValueChange={(itemValue, itemIndex) => {
                this.JuvoPlayer.Log("itemValue = " + itemValue);
                this.pickerChange(itemIndex, "Video");
              }}
              enabled={this.props.visible}>
              {this.props.streamsData.Video.map((item, index) => {
                if (item.Default === true && this.settings.videoSetting === -1) {
                  this.settings.videoSetting = item.Id;
                }
                return <Picker.Item label={item.Description} value={item.Id} key={index} />;
              })}
            </Picker>
            <Text style={{ left: -130, top: -37, color: "#ffffff", fontSize: 28, fontWeight: "bold" }}>Video quality</Text>
            <Picker
              selectedValue={this.settings.subtitleSetting}
              style={{ left: 500, top: -33, height: 30, width: 450, color: "#ffffff" }}
              onValueChange={(itemValue, itemIndex) => {
                this.JuvoPlayer.Log("itemValue = " + itemValue);
                this.pickerChange(itemIndex, "Subtitle");
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
            <Text style={{ left: 340, top: -103, color: "#ffffff", fontSize: 28, fontWeight: "bold" }}>Subtitles</Text>
            <Text style={{ top: -215, left: 0, fontSize: 30, color: "#ffffff", textAlign: "center", fontWeight: "bold" }}>
              {" "}
              Use arrow keys to navigate. Press enter key to select a setting.{" "}
            </Text>
            <Text style={{ top: 0, left: 0, fontSize: 20, color: "#ffffff", textAlign: "center" }}> Press return key to close </Text>
          </View>
        </HideableView>
      </View>
    );
  }
}

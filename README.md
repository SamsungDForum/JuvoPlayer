JuvoPlayer
=======

## Introduction

JuvoPlayer is a reference player library for Tizen TVs written using Tizen .NET technology.
Contrary to Tizen.Multimedia.Player API
(see: https://docs.tizen.org/application/dotnet/guides/multimedia/media-playback/),
JuvoPlayer contains a data downloader and a demuxer inside the application itself.
Thus, it shows how to implement a custom data source.

JuvoPlayer targets Tizen TVs 2019 and newer.

## Differences between 1.* and 2.* versions

Previous version of JuvoPlayer, available on master branch, is the full fledged application.
It contains not only a player library, but also various demo UIs (like Xamarin or React Native).
While JuvoPlayer 1.* version has a lot of features, it may be difficult to analyse and modify
as a whole. In JuvoPlayer 2.*, we decided to move UIs to different repositories and thanks to that,
the codebase is much smaller and easier to read.

Additionally, we decided to publish the JuvoPlayer package on nuget.org, so anybody can reference
it in their application directly from this popular nuget feed.
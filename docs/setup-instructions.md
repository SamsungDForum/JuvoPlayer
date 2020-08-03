[Home](../README.md)

## Setup instructions
1. Download .zip or clone the repository to Your local drive. 
2. Open the JuvoPlayer solution with Microsoft Visual Studio. See the articles regarding Tizen .Net TV environment setup here: [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]

[tizendotnettvlink]: https://developer.samsung.com/tv/tizen-net-tv 

3. Setup the nuget packages repository for the solution (restore NuGet packages)
4. Set one of the UI projects as a StartUp Project. Choose one of the following:
* XamarinPlayer
* JuvoPlayer.OpenGL
* JuvoReactNative
* SimplePlayer

##### Important
> Due to it's 'hybrid' nature, The JuvoReactNative projects requires additional step. Before the first build or any JS part modifications it needs creation of the bundles to be included in the Tizen .NET shared\res folder. See the 'React Native bundle preparation' for details. 
    
### React Native bundle preparation
* Make sure Nodejs and Yarn have been successfully installed in Your system.
* Go to the JuvoReactNative root directory in the command line window.
* Type the `yarn` command without any parameters. Wait until all the dependencies have been installed.
* Release mode
  - Type: `yarn bundle`
  - Go and delete the JuvoReactNative\Tizen\shared\res\assets\assets folder which is redundant (React Native Tizen bundle script issue: https://github.com/Samsung/react-native-tizen-dotnet/issues/30#issue-533804121)
  - Build the JuvoReactNative with MS Visual Studio.
* Debug mode   
  - Follow the 'Release mode' instructions but replace the first step with:  `yarn bundle --dev` command.

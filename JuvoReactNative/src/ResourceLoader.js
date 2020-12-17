import { NativeModules } from 'react-native';

const ResourceLoader = {
    path: 'https://raw.githubusercontent.com/SamsungDForum/JuvoPlayer/master/Resources/',
    clipsData: [],
    errorMessage: '',
    load: () => fetch(ResourceLoader.path + 'videoclips.json').then((response) => response.json())
        .then((responseJson) => {
            ResourceLoader.clipsData = responseJson;
            ResourceLoader.tilePaths = responseJson.map((clip) => ResourceLoader.path + clip.poster);
        })
        .catch((error) => {
            ResourceLoader.errorMessage = error;
        }),
    tilePaths: [],
    contentDescriptionBackground: require('../images/tiles/content_list_bg.png'),
    defaultImage: require('../images/tiles/default_bg.png'),
    playbackIcons: {
        play: require('../images/btn_viewer_control_play_normal.png'),
        ffw: require('../images/btn_viewer_control_forward_normal.png'),
        rew: require('../images/btn_viewer_control_back_normal.png'),
        set: require('../images/btn_viewer_control_settings_normal.png'),
        pause: require('../images/btn_viewer_control_pause_normal.png')
    }
}

export default ResourceLoader;

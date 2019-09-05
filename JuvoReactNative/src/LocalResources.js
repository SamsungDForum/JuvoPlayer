import videoclipsdata from '.././res/videoclips.json'

const LocalResources = {    
    tileNames : [
        'car',
        'bolid',
        'sintel',
        'oops',
        'tosposter',
        'artofwalking',
        'tosposter',
        'bunny',
        'sintel',
        'sacrecoeur',
        'tosposter',
        'canimals',
        'testwatchscreen',
        'bunny'
      ],
    tilesPath: {
        car: require('.././res/images/tiles/carsmall.png'),
        bolid: require('.././res/images/tiles/bolid.png'),
        sintel: require('.././res/images/tiles/sintel.png'),
        oops: require('.././res/images/tiles/oops.png'),
        tosposter: require('.././res/images/tiles/tos-poster.png'),
        artofwalking: require('.././res/images/tiles/artofwalking.png'),
        bunny: require('.././res/images/tiles/bunny.png'),        
        sacrecoeur: require('.././res/images/tiles/sacrecoeur.png'),
        canimals: require('.././res/images/tiles/canimals.png'),
        testwatchscreen: require('.././res/images/tiles/testwatchscreen.png'),        
        default: require('.././res/images/tiles/default_bg.png'),
        contentDescriptionBackground: require('.././res/images/tiles/content_list_bg.png')
    },
    tilePathSelect : name => {
        if (name === null)
          return LocalResources.tilesPath.default;
    
        const tileArray = {
          'car': LocalResources.tilesPath.car,
          'bolid': LocalResources.tilesPath.bolid,
          'sintel': LocalResources.tilesPath.sintel,
          'oops': LocalResources.tilesPath.oops,
          'tosposter': LocalResources.tilesPath.tosposter,
          'artofwalking': LocalResources.tilesPath.artofwalking,
          'bunny': LocalResources.tilesPath.bunny,        
          'sacrecoeur': LocalResources.tilesPath.sacrecoeur,
          'canimals': LocalResources.tilesPath.canimals,
          'testwatchscreen': LocalResources.tilesPath.testwatchscreen,        
          'default': LocalResources.tilesPath.default,
          'contentDescriptionBackground': LocalResources.tilesPath.contentDescriptionBackground
        };    
        if (tileArray[name] == null) return LocalResources.tilesPath.default;
        return tileArray[name];
      },
    clipsData : videoclipsdata,
    playbackIconsPath : {
      'play': require('.././res/images/btn_viewer_control_play_normal.png'),
      'ffw': require('.././res/images/btn_viewer_control_forward_normal.png'),
      'rew': require('.././res/images/btn_viewer_control_back_normal.png'),
      'set': require('.././res/images/btn_viewer_control_settings_normal.png'),
      'pause': require('.././res/images/btn_viewer_control_pause_normal.png')      
    },
    playbackIconsPathSelect : name => {
      if (name === null)
        return LocalResources.tilesPath.default;
  
      const tileArray = {
        'play': LocalResources.playbackIconsPath.play,
        'ffw': LocalResources.playbackIconsPath.ffw,
        'rew': LocalResources.playbackIconsPath.rew,
        'set': LocalResources.playbackIconsPath.set,
        'pause': LocalResources.playbackIconsPath.pause,
        'default': LocalResources.tilesPath.default
      };    
      if (tileArray[name] == null) return LocalResources.tilesPath.default;
      return tileArray[name];
    }    
  };
  
  export default LocalResources;
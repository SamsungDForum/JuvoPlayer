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
        default: require('.././res/images/tiles/default_bg.png')
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
          'default': LocalResources.tilesPath.default
        };    
        if (tileArray[name] == null) return LocalResources.tilesPath.default;
        return tileArray[name];
      },
      clipsData : videoclipsdata      
  };
  
  export default LocalResources;
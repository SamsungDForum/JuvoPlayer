const LocalResources = {
    tileNames : [
        'car',
        'bolid',
        'sintel',
        'oops',
        'tos-poster',
        'artofwalking',
        'tos-poster',
        'bunny',
        'sintel',
        'sacrecoeur',
        'tos-poster',
        'canimals',
        'testwatchscreen',
        'bunny'
      ],
    tilesPath: {
        car: require('.././res/images/tiles/carsmall.png'),
        bolid: require('.././res/images/tiles/bolid.png'),
        sintel: require('.././res/images/tiles/bunny.png'),
        oops: require('.././res/images/tiles/canimals.png'),
        default: require('.././res/images/tiles/default_bg.png')
    },
    tilePathSelect : name => {
        if (name === null)
          return LocalResources.tilesPath.default;
    
        const tileArray = {
          'car': LocalResources.tilesPath.car,
          'bolid': LocalResources.tilesPath.bolid,
          'sintel': LocalResources.tilesPath.sintel,
          'oops': LocalResources.tilesPath.oops
        };    
        if (tileArray[name] == null) return LocalResources.tilesPath.default;
        return tileArray[name];
      }
  };
  
  export default LocalResources;
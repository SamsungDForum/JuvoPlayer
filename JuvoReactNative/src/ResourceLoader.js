import videoclipsdata from "../videoclips.json";

const ResourceLoader = {
  tileNames: ["car", "bolid", "sintel", "oops", "tosposter", "artofwalking", "tosposter", "bunny", "sintel", "sacrecoeur", "tosposter", "canimals", "testwatchscreen", "bunny"],
  tilesPath: {
    car: require("../images/tiles/car.png"),
    bolid: require("../images/tiles/bolid.png"),
    sintel: require("../images/tiles/sintel.png"),
    oops: require("../images/tiles/oops.png"),
    tosposter: require("../images/tiles/tos-poster.png"),
    artofwalking: require("../images/tiles/artofwalking.png"),
    bunny: require("../images/tiles/bunny.png"),
    sacrecoeur: require("../images/tiles/sacrecoeur.png"),
    canimals: require("../images/tiles/canimals.png"),
    testwatchscreen: require("../images/tiles/testwatchscreen.png"),
    default: require("../images/tiles/default_bg.png"),
    contentDescriptionBackground: require("../images/tiles/content_list_bg.png")
  },
  tilePathSelect: name => {
    if (name === null) return ResourceLoader.tilesPath.default;

    const tileArray = {
      car: ResourceLoader.tilesPath.car,
      bolid: ResourceLoader.tilesPath.bolid,
      sintel: ResourceLoader.tilesPath.sintel,
      oops: ResourceLoader.tilesPath.oops,
      tosposter: ResourceLoader.tilesPath.tosposter,
      artofwalking: ResourceLoader.tilesPath.artofwalking,
      bunny: ResourceLoader.tilesPath.bunny,
      sacrecoeur: ResourceLoader.tilesPath.sacrecoeur,
      canimals: ResourceLoader.tilesPath.canimals,
      testwatchscreen: ResourceLoader.tilesPath.testwatchscreen,
      default: ResourceLoader.tilesPath.default,
      contentDescriptionBackground: ResourceLoader.tilesPath.contentDescriptionBackground
    };
    if (tileArray[name] == null) return ResourceLoader.tilesPath.default;
    return tileArray[name];
  },
  clipsData: videoclipsdata,
  playbackIconsPath: {
    play: require("../images/btn_viewer_control_play_normal.png"),
    ffw: require("../images/btn_viewer_control_forward_normal.png"),
    rew: require("../images/btn_viewer_control_back_normal.png"),
    set: require("../images/btn_viewer_control_settings_normal.png"),
    pause: require("../images/btn_viewer_control_pause_normal.png")
  },
  playbackIconsPathSelect: name => {
    if (name === null) return ResourceLoader.tilesPath.default;

    const tileArray = {
      play: ResourceLoader.playbackIconsPath.play,
      ffw: ResourceLoader.playbackIconsPath.ffw,
      rew: ResourceLoader.playbackIconsPath.rew,
      set: ResourceLoader.playbackIconsPath.set,
      pause: ResourceLoader.playbackIconsPath.pause,
      default: ResourceLoader.tilesPath.default
    };
    if (tileArray[name] == null) return ResourceLoader.tilesPath.default;
    return tileArray[name];
  }
};

export default ResourceLoader;

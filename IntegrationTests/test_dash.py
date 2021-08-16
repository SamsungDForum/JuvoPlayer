import pytest
from juvo_test_utils import RestPlayer, Scenario, Action
from video_analysis import VideoAnalyzer
import inspect
import json
from pytest_cases import parametrize_with_cases


@pytest.fixture(scope='session', autouse=True)
def config():
    with open('config.json') as f:
        config = json.load(f)
    yield config


@pytest.fixture(scope='session', autouse=True)
def player_app(config):
    # Will be executed before the first test
    player = RestPlayer(config["player_address"], config["app_name"])
    player.start(config["tv_ip"], config["tv_user"], config["tv_password"])
    yield player
    # Will be executed after the last test
    player.stop()


@pytest.fixture(autouse=True)
def player(player_app):
    yield player_app
    player_app.send({'order': 'stop'})


class MonoResClips:
    def play_order(self):
        return {'order': 'play'}

    def case_plain_dash(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/single_stream/dash/manifest.mpd'
        return clip

    def case_widevine(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/single_stream/widevine/manifest.mpd'
        clip['drm'] = r'''{
            "KeySystem": "com.widevine.alpha",
            "LicenseServerUri": "https://proxy.uat.widevine.com/proxy?provider=widevine_test",
            "RequestHeaders":
            {
                "Content-Type": "text/xml; charset=utf-8"
            }
        }'''
        return clip

    def case_playready(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/single_stream/playready/manifest.mpd'
        clip['drm'] = r'''{
                    "KeySystem" : "com.microsoft.playready",
                    "LicenseServerUri" :
                        "https://test.playready.microsoft.com/service/rightsmanager.asmx?cfg=(kid:header,sl:2000,persist:false,contentkey:EjQSNBI0EjQSNBI0EjQSNA==)",
                    "RequestHeaders" :
                    {
                        "Content-Type" : "text/xml; charset=utf-8"
                    }
                }'''
        return clip


class PolyResClips:
    def play_order(self):
        return {'order': 'play'}

    def case_plain_dash(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/dash/manifest.mpd'
        return clip

    def case_widevine(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/widevine/manifest.mpd'
        clip['drm'] = r'''{
            "KeySystem": "com.widevine.alpha",
            "LicenseServerUri": "https://proxy.uat.widevine.com/proxy?provider=widevine_test",
            "RequestHeaders":
            {
                "Content-Type": "text/xml; charset=utf-8"
            }
        }'''
        return clip

    def case_playready(self):
        clip = self.play_order()
        clip['uri'] = 'http://106.120.45.49/testcontent/playready/manifest.mpd'
        clip['drm'] = r'''{
                    "KeySystem" : "com.microsoft.playready",
                    "LicenseServerUri" :
                        "https://test.playready.microsoft.com/service/rightsmanager.asmx?cfg=(kid:header,sl:2000,persist:false,contentkey:EjQSNBI0EjQSNBI0EjQSNA==)",
                    "RequestHeaders" :
                    {
                        "Content-Type" : "text/xml; charset=utf-8"
                    }
                }'''
        return clip


@parametrize_with_cases("play", cases=MonoResClips)
def test_playback_starts(play, player, config):
    queue = [
        (play, 10)
    ]
    basic_conduct_test(config, player, queue)


@parametrize_with_cases("play", cases=MonoResClips)
def test_playback_stops(play, player, config):
    queue = [
        (play, 10),
        ({'order': 'stop'}, 5)
    ]
    basic_conduct_test(config, player, queue)


@parametrize_with_cases("play", cases=MonoResClips)
def test_playback_pauses(play, player, config):
    queue = [
        (play, 10),
        ({'order': 'pause'}, 5)
    ]
    basic_conduct_test(config, player, queue)


@parametrize_with_cases("play", cases=MonoResClips)
def test_playback_resumes(play, player, config):
    queue = [
        (play, 5),
        ({'order': 'pause', }, 5),
        ({'order': 'resume'}, 2)
    ]
    basic_conduct_test(config, player, queue)


@parametrize_with_cases("play", cases=MonoResClips)
def test_seek_while_playing(play, player, config):
    queue = [
        (play, 10),
        ({'order': 'seek', 'destination': 5, }, 5)
    ]
    basic_conduct_test(config, player, queue)


@parametrize_with_cases("play", cases=PolyResClips)
def test_change_resolution(play, player, config):
    queue = [
        (play, 0),
        ({'order': 'changeVideo', 'width': 1280}, 5),
        ({'order': 'changeVideo', 'width': 1920}, 5)
    ]
    expected_actions = [
        Action('play', 1),
        Action('changeVideo', 5, new_resolution='1920x1080')
    ]
    basic_conduct_test(config, player, queue, expected_actions)


@parametrize_with_cases("play", cases=PolyResClips)
def test_play_4k(play, player, config):
    queue = [
        (play, 0),
        ({'order': 'changeVideo', 'width': 1280}, 5),
        ({'order': 'changeVideo', 'width': 3840}, 5)
    ]
    expected_actions = [
        Action('play', 0),
        Action('changeVideo', 5, new_resolution='3840x2160')
    ]
    basic_conduct_test(config, player, queue, expected_actions)


def basic_conduct_test(config, player, queue, expected_actions=None):
    test_name = inspect.stack()[1].function
    if expected_actions is None:
        expected_actions = orders_to_actions(queue)
    scenario = Scenario(queue, player, name=test_name)
    scenario.run()
    analyzer = VideoAnalyzer(path=f'{test_name}')
    detected_actions = analyzer.run()
    assert compare_actions(
        detected_actions, expected_actions, config["tolerance"])


def orders_to_actions(queue):
    actions = []
    offset = 0
    for order in queue:
        action = order[0]['order']
        if action == 'seek':
            destination = order[0]['destination']
            actions.append(
                Action(action, offset, seek_destination=destination))
        else:
            actions.append(Action(action, offset))
        offset += order[1]
    return actions


def compare_actions(actions, expected_actions, tolerance, seek_tolerance=5):
    if(len(actions) == 0 and len(expected_actions) == 0):
        return True
    if(len(actions) != len(expected_actions)):
        raise ValueError(
            f"Expected {len(expected_actions)} actions, but instead got {len(actions)} actions")

    offset = 0
    if compare_action(actions[0], expected_actions[0], offset, 15, seek_tolerance):
        offset = actions[0].time - expected_actions[0].time

    for i in range(1, len(actions)):
        if(compare_action(actions[i], expected_actions[i], offset, tolerance, seek_tolerance)):
            offset = actions[i].time - expected_actions[i].time
        else:
            return False
    return True


def compare_action(action, expected, offset, tolerance, seek_tolerance):
    if action.name != expected.name:
        raise ValueError(
            f"Expected {expected.name} at {expected.time}, instead got {action.name} at {action.time}")
    mismatch = abs(abs(action.time - expected.time) - offset)

    if mismatch > tolerance:
        raise ValueError(
            f"Expected {expected.name} at {expected.time} tolerance:{tolerance} not met:{mismatch}")

    if action.name == 'seek':
        mismatch_seek = abs(action.seek_destination -
                            expected.seek_destination)
        if mismatch_seek <= seek_tolerance:
            return True
        else:
            raise ValueError(
                f"Expected to seek to {expected.seek_destination}, not to {action.seek_destination}")

    elif action.name == 'changeVideo':
        if action.new_resolution == expected.new_resolution:
            return True
        else:
            raise ValueError(
                f"Expected resolution {expected.new_resolution}, not {action.new_resolution}")
    else:
        return True

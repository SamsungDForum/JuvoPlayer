import pytest
from juvo_test_utils import Recorder,RestPlayer,Scenario,Action
from video_analysis import VideoAnalyzer
import inspect
import json

@pytest.fixture(scope='session', autouse=True)
def config():
    with open('config.json') as f:
        config = json.load(f)
    yield config

@pytest.fixture(scope='session', autouse=True)
def player_app(config):
    # Will be executed before the first test
    player = RestPlayer(player_address = config["player_address"])
    player.start(config["app_name"],config["tv_ip"],config["tv_user"],config["tv_password"])
    yield player
    # Will be executed after the last test
    player.stop()

@pytest.fixture(autouse=True)
def player(player_app):
    yield player_app
    player_app.send({'order': 'stop'})
    
@pytest.fixture(scope='session', autouse=True)
def recorder_session(config):
    recorder = Recorder(ip = config["recorder_ip"], record_delay=12)
    yield recorder

@pytest.fixture(autouse=True)
def recorder(recorder_session):
    yield recorder_session
    recorder_session.cleanup()

def test_dash_playback_starts(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/single_stream/manifest.mpd',
         }, 10)
    ]
    basic_conduct_test(config, player, recorder, queue)

def test_dash_playback_stops(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/single_stream/manifest.mpd',
         }, 10),
        ({'order': 'stop',
                  }, 5)
    ]
    basic_conduct_test(config, player, recorder, queue)

def test_playback_pauses(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/single_stream/manifest.mpd',
         }, 10),
        ({'order': 'pause',
          }, 5)
    ]
    basic_conduct_test(config, player, recorder, queue)

def test_playback_resumes(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/single_stream/manifest.mpd',
         }, 5),
        ({'order': 'pause',
          }, 5),
        ({'order': 'resume',
          }, 2),
        ({'order': 'stop',
          }, 5)
    ]
    basic_conduct_test(config, player, recorder, queue)

def test_seek_while_playing(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/single_stream/manifest.mpd',
         }, 10),
        ({'order': 'seek',
         'destination': 5,
          }, 5)
    ]
    basic_conduct_test(config, player, recorder, queue)

def test_change_resolution(player, recorder, config):
    queue = [
        ({'order': 'play',
         'uri': 'http://106.120.45.49/testcontent/dashtest/manifest.mpd',
         }, 5),
        ({'order': 'changeVideo',
         'index': 0
          }, 5)
    ]
    expected_actions = [
        Action('play',0),
        Action('changeVideo',5, new_resolution='1280x720')
    ]
    basic_conduct_test(config, player, recorder, queue, expected_actions)

def basic_conduct_test(config, player, recorder, queue, expected_actions=None):
    test_name = inspect.stack()[1].function
    if expected_actions is None:
        expected_actions = orders_to_actions(queue)
    scenario = Scenario(queue, player, name=test_name)
    scenario.run(recorder = recorder)
    analyzer = VideoAnalyzer(path=f'{test_name}.mp4')
    detected_actions = analyzer.run()
    assert compare_actions(detected_actions, expected_actions, config["tolerance"])

def orders_to_actions(queue):
    actions = []
    offset = 0
    for order in queue:
        action = order[0]['order']
        if action == 'seek':
            destination = order[0]['destination']
            actions.append(Action(action, offset, seek_destination=destination))
        else:
            actions.append(Action(action, offset))
        offset += order[1]
    return actions

def compare_actions(actions, expected_actions, tolerance, seek_tolerance=5):
    if(len(actions)==0 and len(expected_actions)==0):
        return True
    if(len(actions) != len(expected_actions)):
        raise ValueError(f"{len(actions)} is not equal to {len(expected_actions)}")

    offset = 0
    if compare_action(actions[0], expected_actions[0], offset, 15, seek_tolerance):
        offset = actions[0].time - expected_actions[0].time

    for i in range(1, len(actions)):
        if(compare_action(actions[i], expected_actions[i], offset, tolerance, seek_tolerance)):
            offset = actions[i].time - expected_actions[i].time
        else:
            return False
    return True

def compare_action(a1, a2, offset, tolerance, seek_tolerance):
    if a1.name == a2.name:
        mismatch = abs(abs(a1.time - a2.time) - offset)
        if mismatch <= tolerance:
            if a1.name == 'seek':
                mismatch_seek = abs(a1.seek_destination - a2.seek_destination)
                if mismatch_seek <= seek_tolerance:
                    return True
                else:
                    raise ValueError(f"Expected to seek to {a2.seek_destination}, not to {a1.seek_destination}")
            elif a1.name == 'changeVideo':
                if a1.new_resolution == a2.new_resolution:
                    return True
                else:
                    raise ValueError(f"Expected resolution {a2.new_resolution}, not to {a1.new_resolution}")
            else:
                return True
        else:
            raise ValueError(f"Expected {a2.name} at {a2.time} tolerance:{tolerance} not met:{mismatch}")
    else:
        raise ValueError(f"Expected {a2.name} at {a2.time}, instead got {a1.name} at {a1.name}")
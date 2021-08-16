import glob
import json
import os
import shutil
import time
from junitparser import JUnitXml, Failure, Skipped, Error

import atr_lib
from core.downloader import download_parallel
from core.widget import Widget
from core.rpm import Rpm
from result import Result
from test import Test
import pytest

class JuvoOcrTest(Test):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.tpk_dir = None
        self.tpk_name = None
        self.widget = None
        self.test_name = self.__class__.__name__

    def prepare(self):
        self._resolve_usb_path()
        self.change_dir()
        self._resolve_tpk_path()
        self._prepare_widget()
        return True

    def _resolve_usb_path(self):
        if self.board.file_exists('/opt/media/USBDriveA1'):
            self.usb_path = '/opt/media/USBDriveA1'
        else:
            self.usb_path = '/opt/media/USBDriveB1'

    def change_dir(self):
        script_path = os.path.realpath(__file__)
        dir_path = os.path.dirname(script_path)
        os.chdir(dir_path)

    def _resolve_tpk_path(self):
        script_path = os.path.realpath(__file__)
        self.tpk_dir = os.path.dirname(script_path)
        tpk_path = glob.glob('{}/*.tpk'.format(self.tpk_dir))[0]
        self.tpk_name = os.path.basename(tpk_path)

    def _prepare_widget(self):
        if self.tpk_name is None:
            raise RuntimeError('TPK is missing')
        self.widget = Widget(self.tpk_dir, self.tpk_name, self.board)
        self.widget.install()

    def _prepare_test_config(self):
        script_path = os.path.realpath(__file__)
        script_dir = os.path.dirname(script_path)
        filename = glob.glob('{}/*.json'.format(script_dir))[0]
        with open(filename, 'r') as f:
            config_data = json.load(f)
            config_data['tv_ip'] = self.board.ip
            config_data['player_address'] = config_data['player_address'].replace('localhost', self.board.ip)
            config_data['tv_user'] = os.getenv('BOARD_USER')
            config_data['tv_password'] = os.getenv('BOARD_PASS')

        os.remove(filename)
        with open(filename, 'w') as f:
            json.dump(config_data, f, indent=4)

    def _get_results(self):
        if not os.path.isfile(f'{self.test_name}.xml'):
            if self._has_crashed():
                self._pull_core_dump_files()
            raise RuntimeError('Tests results are missing')

        xml = JUnitXml.fromfile(f'{self.test_name}.xml')
        for suite in xml:
            for case in suite:
                result = True
                if case.result:
                    if isinstance(case.result, Failure):
                        result = False
                    if isinstance(case.result, Skipped):
                        continue

                name = case.name
                self.results.add_results(Result(name, result=result))
                if not result:
                    self.results.result = result

    def _has_crashed(self):
        if self.widget.pid is None:
            return False
        version, build = self.board.get_image_ver()
        result = self.board.exe_cmd_with_result(
            'ls {}/Coredump*{}*{}*'.format(self.usb_path, self.widget.pid,
                                           build))
        if result.return_code != 0:
            return False
        return len(result.stdout.split()) == 1

    def _pull_core_dump_files(self):
        # Sometimes, this method may pull more files than needed,
        # but it shouldn't be a problem
        result = self.board.exe_cmd_with_result(
            'ls {}/*{}*'.format(self.usb_path, self.widget.pid))
        if result.return_code != 0:
            pass
        for path in result.stdout.split():
            self.board.pull_file(path, self.result_path)

    def run(self):
        try:
            self.widget.launch()
            self._prepare_test_config()
            pytest.main(['-x','test_dash.py','--junitxml',f'{self.test_name}.xml'])
        finally:
            if self.widget.check_if_running():
                self.widget.terminate()
            self.widget.uninstall()
            self._get_results()
            return self.results.result

    def collect_results(self):
        return self.results

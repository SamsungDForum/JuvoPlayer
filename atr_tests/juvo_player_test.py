import glob
import json
import os
import shutil
import time
import xml.etree.ElementTree as ET

import atr_lib
from core.downloader import download_parallel
from core.tv_board import TvBoard
from core.widget import Widget
from result import Result
from test import Test


class JuvoPlayerTest(Test):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.tpk_dir = None
        self.tpk_name = None
        self.widget = None

    def prepare(self):
        self._resolve_tpk_path()
        self._prepare_widget()
        return True

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

    def _get_results(self):
        result_paths = self.board.exe_cmd_with_result('ls /tmp/Juvo*.xml')
        if result_paths.return_code != 0:
            if self._has_crashed():
                self._pull_core_dump_files()
            raise RuntimeError('Tests results are missing')

        for result_path in result_paths.stdout.split():
            self.board.pull_file(result_path, self.result_path)
            basename = os.path.basename(result_path)
            local_result_path = os.path.join(self.result_path, basename)
            root = ET.parse(local_result_path).getroot()
            for test_case in root.iter('test-case'):
                if test_case.attrib['result'] == 'Skipped':
                    continue
                name = test_case.attrib['name']
                result = test_case.attrib['result'] == 'Passed'
                self.results.add_results(Result(name, result=result))
                if not result:
                    self.results.result = result

    def _has_crashed(self):
        if self.widget.pid is None:
            return False
        pid = self.widget.pid
        version, build = self.board.get_image_ver()
        for mount_point in TvBoard.UsbStorage(self.board).drive_path():
            result = self.board.exe_cmd_with_result(
                'ls {}/Coredump*{}*{}*'.format(mount_point, pid,
                                            build))
            if result.return_code == 0 and len(result.stdout.split()) == 1:
                return True
        return False

    def _pull_core_dump_files(self):
        # Sometimes, this method may pull more files than needed,
        # but it shouldn't be a problem
        pid = self.widget.pid
        for mount_point in TvBoard.UsbStorage(self.board).drive_path():
            result = self.board.exe_cmd_with_result(
                'ls {}/*{}*'.format(mount_point, pid))
            if result.return_code != 0:
                continue
            for path in result.stdout.split():
                self.board.pull_file(path, self.result_path)

    def run(self):
        try:
            self.widget.launch()
            while self.widget.check_if_running():
                time.sleep(5)
        finally:
            if self.widget.check_if_running():
                self.widget.terminate()
            self.widget.uninstall()
            self._get_results()
            return self.results.result

    def collect_results(self):
        return self.results

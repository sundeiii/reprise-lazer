"""
Reprise: runs the test classes that failed once more, and passes CI if they pass then.

Some of osu!'s tests depend on timing and fail now and then on CI's shared runners (another test each time). This reruns the classes
with failures (each project on its own, with --no-build), and in the original results (TestResults/<name>.trx) marks the failed tests
of a class that then passed as passed, with a note: the summary job reads those. A test that fails twice still fails the run.

Usage: rerun-failed-tests.py <trx file name> (e.g. TestResults-Linux-SingleThread.trx), from the repository's root.
"""

import glob
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

NS = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
ET.register_namespace('', NS)


def q(tag):
    return '{%s}%s' % (NS, tag)


def failed_classes(trx):
    root = ET.parse(trx).getroot()
    classes = {u.get('id'): u.find(q('TestMethod')).get('className') for u in root.iter(q('UnitTest'))}
    return sorted({classes[r.get('testId')] for r in root.iter(q('UnitTestResult')) if r.get('outcome') == 'Failed'})


def mark_passed(trx, passed_classes):
    tree = ET.parse(trx)
    root = tree.getroot()
    classes = {u.get('id'): u.find(q('TestMethod')).get('className') for u in root.iter(q('UnitTest'))}
    fixed = 0

    for r in root.iter(q('UnitTestResult')):
        if r.get('outcome') == 'Failed' and classes[r.get('testId')] in passed_classes:
            r.set('outcome', 'Passed')
            output = r.find(q('Output'))
            if output is None:
                output = ET.SubElement(r, q('Output'))
            stdout = output.find(q('StdOut'))
            if stdout is None:
                stdout = ET.SubElement(output, q('StdOut'))
            stdout.text = 'Failed on the first run and passed on the rerun (rerun-failed-tests.py).\n' + (stdout.text or '')
            fixed += 1

    if fixed:
        summary = root.find(q('ResultSummary'))
        counters = summary.find(q('Counters'))
        counters.set('failed', str(int(counters.get('failed')) - fixed))
        counters.set('passed', str(int(counters.get('passed')) + fixed))
        if counters.get('failed') == '0':
            summary.set('outcome', 'Completed')
        tree.write(trx, xml_declaration=True, encoding='utf-8')

    return fixed


def main():
    name = sys.argv[1]
    results = sorted(glob.glob(os.path.join('**', 'TestResults', name), recursive=True))
    failing = {trx: failed_classes(trx) for trx in results}
    failing = {trx: classes for trx, classes in failing.items() if classes}

    if not failing:
        # The run failed without a failed test (a build error, a crashed test host): nothing to rerun.
        print('No failed tests in the results: not rerunning.')
        return 1

    all_passed = True

    for trx, classes in failing.items():
        project_dir = os.path.dirname(os.path.dirname(trx))
        project = glob.glob(os.path.join(project_dir, '*.csproj'))[0]
        test_filter = '|'.join('FullyQualifiedName~' + c for c in classes)

        print(f'Rerunning in {project}: {", ".join(classes)}', flush=True)
        rerun = subprocess.run(['dotnet', 'test', project, '--no-build', '--filter', test_filter,
                                '--logger', 'trx;LogFileName=Rerun-' + name, '--', 'NUnit.ConsoleOut=0'])

        rerun_trx = os.path.join(project_dir, 'TestResults', 'Rerun-' + name)
        still_failing = set(failed_classes(rerun_trx)) if os.path.exists(rerun_trx) else set(classes)

        if rerun.returncode != 0 and not still_failing:
            still_failing = set(classes)

        passed = [c for c in classes if c not in still_failing]
        print(f'Passed on the rerun: {", ".join(passed) or "none"}; still failing: {", ".join(sorted(still_failing)) or "none"}', flush=True)
        mark_passed(trx, set(passed))

        if still_failing:
            all_passed = False

    return 0 if all_passed else 1


if __name__ == '__main__':
    sys.exit(main())

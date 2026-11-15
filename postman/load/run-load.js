#!/usr/bin/env node
// Volume/concurrency test for the Employee Sync Service.
//
// Generates <count> employee rows (mixed provider1/provider2; ~5% TrackTik failures, ~5% invalid),
// splits them across <workers> Newman runs executed in parallel, then compares the database totals
// (GET /api/v1/reports/sync-status before vs after) with what the data should have produced.
//
// Usage (from postman/, stack started with docker-compose.postman.yml):
//   npm run load -- --count 2000 --workers 4
// Options: --count, --workers, --base-url (default http://localhost:8081), --max-ms (per-request SLA, default 2000)
// Output:  load/out/<runId>/report-wN.html, junit-wN.xml, summary.json. Exit code 1 if anything failed.

const fs = require('fs');
const path = require('path');
const newman = require('newman');

const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, arg, i, all) => {
  if (arg.startsWith('--')) pairs.push([arg.slice(2), all[i + 1]]);
  return pairs;
}, []));
const count = Number(args.count ?? 1000);
const workers = Math.max(1, Number(args.workers ?? 4));
const baseUrl = (args['base-url'] ?? 'http://localhost:8081').replace(/\/$/, '');
const maxMs = String(args['max-ms'] ?? 2000);
const runId = Date.now().toString();
const outDir = path.join(__dirname, 'out', runId);
const collection = path.join(__dirname, 'ManagerEmployee.load.postman_collection.json');
const statuses = ['active', 'inactive', 'terminated'];

function generateRows() {
  return Array.from({ length: count }, (_, i) => ({
    seq: i + 1,
    provider: i % 2 === 0 ? 'provider1' : 'provider2',
    kind: i % 20 === 7 ? 'fail' : i % 20 === 13 ? 'invalid' : 'ok',
    status: statuses[i % statuses.length]
  }));
}

async function getReport() {
  const response = await fetch(`${baseUrl}/api/v1/reports/sync-status`);
  if (!response.ok) throw new Error(`Report endpoint returned ${response.status}; is the stack running at ${baseUrl}?`);
  return response.json();
}

function runWorker(index, rows) {
  const dataFile = path.join(outDir, `data-w${index}.json`);
  fs.writeFileSync(dataFile, JSON.stringify(rows));
  return new Promise((resolve, reject) => {
    newman.run({
      collection,
      iterationData: dataFile,
      envVar: [
        { key: 'baseUrl', value: baseUrl },
        { key: 'runId', value: runId },
        { key: 'maxMs', value: maxMs }
      ],
      reporters: ['htmlextra', 'junit'],
      reporter: {
        htmlextra: { export: path.join(outDir, `report-w${index}.html`), title: `ManagerEmployee load - worker ${index}`, showOnlyFails: true },
        junit: { export: path.join(outDir, `junit-w${index}.xml`) }
      }
    }, (error, summary) => {
      if (error) return reject(error);
      const { stats, timings } = summary.run;
      resolve({
        worker: index,
        rows: rows.length,
        requests: stats.requests.total,
        failedRequests: stats.requests.failed,
        assertions: stats.assertions.total,
        failedAssertions: stats.assertions.failed,
        avgResponseMs: Math.round(timings.responseAverage),
        maxResponseMs: Math.round(timings.responseMax),
        firstFailures: summary.run.failures.slice(0, 5).map(f => `${f.source?.name}: ${f.error?.test ?? ''} ${f.error?.message ?? ''}`.trim())
      });
    });
  });
}

(async () => {
  fs.mkdirSync(outDir, { recursive: true });
  const rows = generateRows();
  const expected = {
    synced: rows.filter(r => r.kind === 'ok').length,
    failed: rows.filter(r => r.kind === 'fail').length,
    invalid: rows.filter(r => r.kind === 'invalid').length
  };
  const slices = Array.from({ length: workers }, (_, w) => rows.filter((_, i) => i % workers === w));

  console.log(`Run ${runId}: ${count} employees (${expected.synced} ok, ${expected.failed} TrackTik failures, ${expected.invalid} invalid), ${workers} parallel workers, ${count * 3} API requests`);
  const before = await getReport();
  const started = Date.now();
  const results = await Promise.all(slices.map((slice, w) => runWorker(w + 1, slice)));
  const elapsedSec = (Date.now() - started) / 1000;
  const after = await getReport();

  const delta = key => after[key] - before[key];
  const dbChecks = [
    ['synced rows added', delta('synced'), expected.synced],
    ['failed rows added', delta('failed'), expected.failed],
    ['pending rows added (nothing stuck)', delta('pending'), 0],
    ['total rows added (no duplicates, invalid not stored)', delta('total'), expected.synced + expected.failed]
  ].map(([check, actual, wanted]) => ({ check, actual, expected: wanted, pass: actual === wanted }));

  const totals = results.reduce((t, r) => ({
    requests: t.requests + r.requests,
    failedAssertions: t.failedAssertions + r.failedAssertions,
    assertions: t.assertions + r.assertions
  }), { requests: 0, failedAssertions: 0, assertions: 0 });
  const passed = totals.failedAssertions === 0 && results.every(r => r.failedRequests === 0) && dbChecks.every(c => c.pass);

  console.log('\nWorkers');
  console.table(results.map(({ firstFailures, ...r }) => r));
  console.log('Database totals (sync-status report, before -> after)');
  console.table(dbChecks);
  console.log(`Throughput: ${(totals.requests / elapsedSec).toFixed(1)} req/s over ${elapsedSec.toFixed(1)} s`);
  results.filter(r => r.firstFailures.length).forEach(r => console.log(`Worker ${r.worker} first failures:\n  ${r.firstFailures.join('\n  ')}`));

  const summary = { runId, count, workers, baseUrl, maxMs: Number(maxMs), elapsedSec, throughputRps: totals.requests / elapsedSec, expected, before, after, dbChecks, workersResults: results, passed };
  fs.writeFileSync(path.join(outDir, 'summary.json'), JSON.stringify(summary, null, 2));
  console.log(`\n${passed ? 'PASSED' : 'FAILED'} - reports in ${outDir}`);
  process.exitCode = passed ? 0 : 1;
})().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});

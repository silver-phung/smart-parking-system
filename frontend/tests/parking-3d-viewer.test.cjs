const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../assets/js/parking-3d-viewer.js'), 'utf8');

async function load(fetch) {
  const sandbox = { module: { exports: {} }, URL, Date, fetch, location: { origin: 'http://localhost' }, window: { console: null }, setTimeout: () => 0 };
  vm.runInNewContext(source, sandbox);
  const viewer = { options: { lotId: 2, apiEndpoint: '/api/v1/parking-floors/{floorId}/slots' }, rows: null, states: [],
    _renderSlots(rows) { this.rows = rows; } };
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Viewer request did not finish')), 2000);
    viewer._setDataState = function (state) {
      this.states.push(state);
      if (state !== 'loading') { clearTimeout(timer); resolve(); }
    };
    sandbox.module.exports.prototype.loadSlots.call(viewer, '17');
  });
  return viewer;
}

test('API viewer loads all slot pages for the selected floor', async () => {
  const urls = [];
  const viewer = await load(async url => {
    urls.push(url);
    const page = Number(new URL(url, 'http://local').searchParams.get('pageNumber'));
    return { ok: true, json: async () => ({ data: { items: [{ id: page }], totalPages: 2 } }) };
  });
  assert.deepEqual(Array.from(viewer.rows, row => row.id), [1, 2]);
  assert.equal(urls.length, 2);
  assert.ok(urls.every(url => url.startsWith('/api/v1/parking-floors/17/slots?')));
  assert.equal(viewer.states.at(-1), 'fresh');
});

test('API viewer reports failed reads without generating sample slots', async () => {
  const viewer = await load(async () => ({ ok: false, status: 503 }));
  assert.equal(viewer.rows.length, 0);
  assert.equal(viewer.states.at(-1), 'error');
});

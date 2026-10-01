const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const existingType = () => 7;
const order = { 'shared-existing-asc': existingType };
const window = { jQuery: { fn: { dataTable: { ext: { type: { order } } } } } };
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../ICP/wwwroot/js/tariff-protable.js'), 'utf8'), { window });
const definitions = window.TariffProTable.buildTariffSortColumnDefs([
    { fieldName: 'Hidden', visible: false },
    { fieldName: 'ImportDate' }, { fieldName: 'MAWB' }, { FieldName: 'HAWB' },
    { fieldName: 'InvoiceNumber' }, { fieldName: 'LineNo' }, { fieldName: 'DescriptionOfGoods' }
]);
assert.equal(JSON.stringify(definitions), '[{"targets":[1,2,3,4],"type":"tariff-natural-v1"}]');
assert.equal(order['shared-existing-asc'], existingType);
assert.deepEqual(Object.keys(order).sort(), ['shared-existing-asc', 'tariff-natural-v1-asc', 'tariff-natural-v1-desc']);

const asc = order['tariff-natural-v1-asc'];
const desc = order['tariff-natural-v1-desc'];
for (const [first, second] of [
    ['2', '10'], ['M2', 'M10'], ['H2', 'H10'], ['INV2', 'INV10'], ['A2B2', 'A2B10'],
    ['A9007199254740992', 'A9007199254740993'],
    ['A999999999999999999999999999999', 'A1000000000000000000000000000000'],
    ['<img src=x onerror=throw>2', '<img src=x onerror=throw>10']
]) {
    assert.ok(asc(first, second) < 0, `${first} before ${second}`);
    assert.ok(desc(first, second) > 0, `${first} after ${second} descending`);
}
assert.equal(asc('A002B02', 'a2b2'), 0);
assert.equal(asc('A000', 'a0'), 0);
assert.equal(asc(null, ''), 0);
assert.deepEqual(['A002', 'a2', 'A02'].sort(asc), ['A002', 'a2', 'A02']);
assert.equal(JSON.stringify(window.TariffProTable.buildTariffSortColumnDefs([
    { fieldName: 'MAWB', visible: false }, { fieldName: 'DescriptionOfGoods' }
])), '[]');
console.log('Tariff JavaScript natural sorting checks passed.');

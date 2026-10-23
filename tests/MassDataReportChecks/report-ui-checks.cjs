const fs=require('node:fs'), vm=require('node:vm'), assert=require('node:assert/strict'), path=require('node:path');
let hasDetails=false;
function $(selector) { return {length: selector==='#reportDetailDataDiv' && hasDetails ? 1 : 0}; }
const window={jQuery:$};
let source=fs.readFileSync(path.resolve(__dirname,'../../ICP/wwwroot/js/report-data.js'),'utf8');
source=source.replace('})(window, window.jQuery);','global.testSort=resolveInitialSort; })(window, window.jQuery);');
vm.runInNewContext(source,{window});
const config={headerFields:[{fieldName:'InvoiceNo',visible:true,displayOrder:1},{fieldName:'Owner',visible:true,displayOrder:2}],headerInitialSort:{fieldName:'CreateTime',direction:'desc'}};
window.ReportDataApp.state.pageConfig=config;
assert.equal(JSON.stringify(window.testSort('header',config)),'[]');
config.headerInitialSort={fieldName:'Owner',direction:'asc'};
assert.equal(JSON.stringify(window.testSort('header',config)),'[[1,"asc"]]');
hasDetails=true;
assert.equal(JSON.stringify(window.testSort('header',config)),'[[2,"asc"]]');
console.log('PASS hidden CreateTime preserves server order; Header-only sort offset; split report sort offset unchanged');

import fs from 'fs';

try {
    const jsonString = fs.readFileSync('test-results.json', 'utf8').replace(/^\uFEFF/, '');
    const data = JSON.parse(jsonString);
    let csv = 'Suite,Test Case,Status,Duration (ms)\n';

    function extractSpecs(suite, suiteName) {
        if (suite.specs) {
            suite.specs.forEach(sp => {
                const status = sp.tests[0]?.results[0]?.status || 'unknown';
                const duration = sp.tests[0]?.results[0]?.duration || 0;
                csv += `"${suiteName}","${sp.title}","${status}",${duration}\n`;
            });
        }
        if (suite.suites) {
            suite.suites.forEach(s => extractSpecs(s, suiteName ? suiteName + ' > ' + s.title : s.title));
        }
    }

    data.suites.forEach(s => extractSpecs(s, s.title));
    fs.writeFileSync('TestResults.csv', csv);
    console.log('CSV created successfully.');
} catch (e) {
    console.error('Error generating CSV:', e);
}

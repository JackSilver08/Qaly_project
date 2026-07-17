import fs from 'fs';
import * as xlsx from 'xlsx';

xlsx.set_fs(fs);

// Convert TestResults.csv to TestResults.xlsx
if (fs.existsSync('TestResults.csv')) {
    const csvData = fs.readFileSync('TestResults.csv', 'utf8');
    const wb = xlsx.read(csvData, {type: 'string'});
    xlsx.writeFile(wb, 'TestResults.xlsx');
    console.log('Created TestResults.xlsx');
}

// Convert TestCases.csv to TestCases.xlsx
if (fs.existsSync('TestCases.csv')) {
    const csvData2 = fs.readFileSync('TestCases.csv', 'utf8');
    const wb2 = xlsx.read(csvData2, {type: 'string'});
    xlsx.writeFile(wb2, 'TestCases.xlsx');
    console.log('Created TestCases.xlsx');
}

// Test JavaScript file for MIME type detection
console.log('VNLib.WebServer JavaScript test file loaded');

const vnlibTest = {
    server: 'VNLib.WebServer',
    testType: 'MIME Detection',
    timestamp: new Date().toISOString(),
    
    // Test function
    runTests: function() {
        console.log('Running VNLib.WebServer tests...');
        return {
            success: true,
            message: 'JavaScript file served correctly',
            contentType: 'Expected: application/javascript or text/javascript'
        };
    },
    
    // Test various JavaScript features
    testFeatures: function() {
        const features = {
            es6: typeof Symbol !== 'undefined',
            promises: typeof Promise !== 'undefined',
            fetch: typeof fetch !== 'undefined',
            localStorage: typeof localStorage !== 'undefined'
        };
        
        console.log('Browser features:', features);
        return features;
    }
};

// Self-executing test
if (typeof window !== 'undefined') {
    vnlibTest.runTests();
    vnlibTest.testFeatures();
} else {
    // Node.js environment
    module.exports = vnlibTest;
}

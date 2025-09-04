// VNLib.WebServer Test JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Display current time to show dynamic content loading
    const timeElement = document.getElementById('server-time');
    if (timeElement) {
        timeElement.textContent = new Date().toLocaleString();
    }
    
    // Add click handlers for test links
    const testLinks = document.querySelectorAll('nav a');
    testLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            console.log('Test link clicked:', this.href);
        });
    });
    
    // Test console output
    console.log('VNLib.WebServer test page loaded successfully');
    console.log('User agent:', navigator.userAgent);
    console.log('Current URL:', window.location.href);
});

// Test function for HTTP method testing
function testHttpMethods() {
    console.log('Testing HTTP methods...');
    
    // This will be useful for method testing later
    return {
        userAgent: navigator.userAgent,
        timestamp: Date.now(),
        url: window.location.href
    };
}


/// https://stackoverflow.com/a/14368860/338
function deparam(querystring) {
    querystring = querystring.substring(querystring.indexOf('?')+1).split('&');
    var params = {}, pair, d = decodeURIComponent;
    for (var i = querystring.length - 1; i >= 0; i--) {
        pair = querystring[i].split('=');
        params[d(pair[0])] = d(pair[1] || '');
    }
    return params;
};

function addPackageToMru(id, iconUrl)
{
}

function showLastQuery()
{
    var ps = deparam(document.location.toString());
    if (ps.hasOwnProperty("q")) {
        localStorage["q"] = ps["q"];
    }
    if (localStorage.hasOwnProperty("q")) {
        document.getElementById("packageSearch").value = localStorage["q"];
    }
}

$(function() {
    $('.dropdown-toggle').dropdown();

    addPackageToMru ();
    showLastQuery ();
});


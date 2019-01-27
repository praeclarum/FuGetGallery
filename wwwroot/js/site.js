
/// https://stackoverflow.com/a/14368860/338
function deparam(querystring) {
    querystring = querystring.substring(querystring.indexOf('?')+1).split('&');
    var params = {}, pair, d = decodeURIComponent;
    for (var i = querystring.length - 1; i >= 0; i--) {
        pair = querystring[i].split('=');
        params[d(pair[0])] = d(pair[1] || '').replace("+", " ");
    }
    return params;
};

function addPackageToMru()
{
    if (typeof packageId === "undefined") return;
    var mru = (localStorage.getItem("mru") !== null) ? JSON.parse(localStorage["mru"]) : {};
    if (typeof mru !== "object") mru = {};
    var p;
    if (mru.hasOwnProperty(packageId)) {
        p = mru[packageId];
    }
    else {
        p = {id: packageId, count: 0, lastTime: new Date()};
        mru[packageId] = p;
    }
    if (mru["__lastPackageId"] !== packageId) {
        p.count++;
        p.lastTime = new Date();
        p.iconUrl = packageIconUrl;
        p.authors = packageAuthors;
        p.description = packageDescription;
        mru["__lastPackageId"] = packageId;
        localStorage["mru"] = JSON.stringify(mru);
    }
}

function renderMru(id, title, sorter)
{
    var $mru = $(id);
    if ($mru.length === 0) return;

    var i, x;
    var mru = (localStorage.getItem("mru") !== null) ? JSON.parse(localStorage["mru"]) : {};
    var mrus = [];
    for (i in mru) {
        if (i === "__lastPackageId" || !mru.hasOwnProperty(i)) continue;
        x = mru[i];
        x.lastTime = Date.parse(x.lastTime);
        mrus.push(x);
    }
    mrus = mrus.sort(sorter);

    $mru.append("<h3 style=\"margin-top:2em;margin-bottom:1em;\">"+title+"</h3>");
    if (mrus.length === 0)
        $mru.append("<i>The packages that you visit will be listed here. Try searching at the top to get started.</i>");
    var $ul = $("<ul class='media-list'></ul>");
    $mru.append($ul);
    for (i = 0; i < mrus.length && i < 20; i++) {
        var p = mrus[i];
        var $li = $("<li class='media'></li>");
        var $left = $("<div class=\"media-left\"/>");
        $left.append($("<a/>").attr("href", "/packages/" + encodeURIComponent(p.id)).append($("<img class='package-icon-in-list' width='64' height='64' onError=\"this.onerror=null;this.src='/images/no-icon.png';\" />").attr("src", p.iconUrl)));
        var $body = $("<div class='media-body'></div>");
        var $h = $("<h4></h4>");
        $h.append($("<a/>").attr("href", "/packages/" + encodeURIComponent(p.id)).text(p.id));
        $h.append($("<small/>").text(" by " + p.authors));
        $body.append($h);
        $body.append($("<p style=\"overflow:auto\">").text(p.description));
        $li.append($left);
        $li.append($body);
        $ul.append($li);
    }
}

function showLastQuery()
{
    var ps = deparam(document.location.toString());
    if (ps.hasOwnProperty("q") && (""+ps["q"]).length > 0) {
        localStorage["q"] = ps["q"];
    }
    if (localStorage.hasOwnProperty("q")) {
        document.getElementById("packageSearch").value = localStorage["q"];
    }
}

$(function() {
    $('.dropdown-toggle').dropdown();
    showLastQuery ();
    addPackageToMru ();
    renderMru ("#rmru", "Your Recent Packages", function(x,y) { 
        return y.lastTime - x.lastTime;
    });
    renderMru ("#cmru", "Your Common Packages", function(x,y) { return y.count - x.count; });
});


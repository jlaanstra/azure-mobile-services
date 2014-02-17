/// <reference path="mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');
var conflicts = require('../shared/conflicts');

function update(item, user, request) {

    //existing items must have a timestamp
    if (item.__version === undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "update operation must have __version");
        return;
    }
    //item cannot set isDeleted
    if (item.isDeleted !== undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "item cannot set isDeleted, isDeleted is a reserved column name");
        return;
    }

    var processResult = function (results) {
        console.log(results);

        if (!Array.isArray(results)) {
            results = [results];
        }

        var nondeleted = [];
        var deleted = [];
        results.map(function (r) {
            if (!r.isDeleted) {
                nondeleted.push(r);
            } else {
                deleted.push(r.guid);
            }
        });
        responseHelper.sendSuccessResponse(request, "", nondeleted, deleted, {});
    }

    var processResolution = function (results) {
        if (!Array.isArray(results)) {
            results = [results];
        }

        for (var item in results) {
            delete item.__version;
            tables.current.update(item, {
                success: function () { },
                error: function (err) {
                    console.error("Error occurred. Details:", err);
                    request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                }
            });
        }

        processResult(results);
    }

    request.execute({
        systemProperties: ['*'],
        success: function () {
            processResult(item);
        },
        conflict: function (serverItem) {
            processResolution(conflicts.resolveConflictOnServer(serverItem, item, conflicts.latestWriteWins));
            //conflicts.resolveConflictOnClient(request, serverItem, item);
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });

}
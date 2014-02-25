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

    //configurable conflict resolution
    var resolveStrategy = request.parameters.resolveStrategy;
    if (resolveStrategy === undefined) {
        resolveStrategy = "latestWriteWins";
    }

    request.execute({
        systemProperties: ['*'],
        success: function () {
            responseHelper.sendSuccessResponse(request, "", [item], [], {});
        },
        conflict: function (serverItem) {
            item.isDeleted = false;

            // determine conflict type
            var type = conflicts.getConflictType(serverItem, item);
            //configurable conflict resolution
            if (resolveStrategy === "client") {
                conflicts.resolveConflictOnClient(request, serverItem, item, type);
            }
            else {
                var results = conflicts.resolveConflictOnServer(serverItem, item, conflicts[resolveStrategy]);
                conflicts.processResult(request, results, tables.current, type, resolveStrategy);
            }
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}
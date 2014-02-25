/// <reference path="../mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');
var conflicts = require('../shared/conflicts');

function del(id, user, request) {

    var requestVersion = request.parameters.version;
    //existing items must have a timestamp
    if (requestVersion === undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "delete operation must specify the version parameter");
        return;
    }

    var tableName = tables.current.getTableName();

    //configurable conflict resolution
    var resolveStrategy = request.parameters.resolveStrategy;
    if (resolveStrategy === undefined) {
        resolveStrategy = "latestWriteWins";
    }

    //only delete if false to safe an unnecessary changes
    var sqlUpdate = "UPDATE " + tableName + " SET isDeleted = 'True' WHERE id = ?";
    var sqlSelect = "SELECT * FROM " + tableName + " WHERE id = ?";

    mssql.query(sqlSelect, id, {
        success: function (results) {
            if (results.length > 0) {
                //server and client know about the same item
                var requestVersionBuffer = new Buffer(requestVersion, 'base64');
                var comparison = compare(results[0].__version, requestVersionBuffer);
                console.log(comparison);
                if (comparison === 0) {
                    mssql.query(sqlUpdate, id, {
                        success: function () {
                            responseHelper.sendSuccessResponse(request, "", [], [id], {});
                        },
                        error: function (err) {
                            console.error("Error occurred. Details:", err);
                            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                        }
                    });
                    // server item is newer than client item: CONFLICT
                } else if (comparison > 0) {
                    var newItem = {}
                    for (var item in results[0]) {
                        newItem[item] = results[0][item];
                    }
                    newItem.isDeleted = true;

                    // determine conflict type
                    var type = conflicts.getConflictType(results[0], newItem);
                    //configurable conflict resolution
                    if (resolveStrategy === "client") {
                        conflicts.resolveConflictOnClient(request, results[0], newItem, type);
                    }
                    else {
                        var results = conflicts.resolveConflictOnServer(results[0], newItem, conflicts[resolveStrategy]);
                        conflicts.processResult(request, results, tables.current, type, resolveStrategy);
                    }
                } else {
                    //client item is newer, which should never happen anyway.
                    console.log(results[0].__version.toString('base64'));
                    console.log(requestVersion);
                    request.respond(statusCodes.BAD_REQUEST, "item is newer than known by the server.");
                }
            }
            else {
                // if item cannot be found it might as well be deleted
                responseHelper.sendSuccessResponse(request, "", [], [id], {});
            }
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}

function compare(cmp, to) {
    var c = 0;
    for (var i = 0; i < cmp.length; ++i) {
        if (i == to.length) {
            break;
        }
        c = cmp[i] < to[i] ? -1 : cmp[i] > to[i] ? 1 : 0;
        if (c != 0) {
            break;
        }
    }
    if (c == 0) {
        if (to.length > cmp.length) {
            c = -1;
        }
        else if (cmp.length > to.length) {
            c = 1;
        }
    }
    return c;
}

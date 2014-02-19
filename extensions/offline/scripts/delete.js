/// <reference path="../mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');
var conflicts = require('../shared/conflicts');
var async = require('async');

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

    var processResult = function (results) {
        if (!Array.isArray(results)) {
            results = [results];
        }

        function updateItem(item, callback) {
            delete item.__version;
            tables.current.update(item, {
                systemProperties: ['*'],
                success: function () {
                    callback(null, item);
                },
                error: function (err) {
                    callback(err, null);
                }
            });
        }

        function done(err, updatedResults) {
            if (err !== null) {
                console.error("Error occurred. Details:", err);
                request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
            }
            else {
                var nondeleted = [];
                var deleted = [];
                updatedResults.map(function (r) {
                    if (!r.isDeleted) {
                        nondeleted.push(r);
                    } else {
                        deleted.push(r.id);
                    }
                });
                responseHelper.sendSuccessResponse(request, "", nondeleted, deleted, { conflictResolved: resolveStrategy });
            }
        }

        async.map(results, updateItem, done);
    }

    mssql.query(sqlSelect, id, {
        success: function (results) {
            if (results.length > 0) {
                //server and client know about the same item
                if (results[0].__version.toString('base64') === requestVersion) {
                    mssql.query(sqlUpdate, id, {
                        success: function () {
                            responseHelper.sendSuccessResponse(request, "", [], [id], {});
                        },
                        error: function (err) {
                            console.error("Error occurred. Details:", err);
                            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                        }
                    });
                    // server item is newer than client item
                } else if (results[0].__version.toString('base64') > requestVersion) {
                    var newItem = {}
                    for (var item in results[0]) {
                        newItem[item] = results[0][item];
                    }
                    newItem.isDeleted = true;

                    //configurable conflict resolution
                    if (resolveStrategy === "client") {
                        conflicts.resolveConflictOnClient(request, results[0], newItem);
                    }
                    else {
                        processResult(conflicts.resolveConflictOnServer(results[0], newItem, conflicts[resolveStrategy]));
                    }
                } else {
                    //client item is newer, which should never happen anyway.
                    request.respond(statusCodes.BAD_REQUEST);
                }
            }
            else {
                responseHelper.sendSuccessResponse(request, "", [], [id], {});
            }
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}
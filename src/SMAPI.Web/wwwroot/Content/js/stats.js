var smapi = smapi || {};
var app;
smapi.statsPage = function (options) {
    /*********
    ** Configure
    *********/
    const config = {
        // default colors for chart series (which cycle through the list)
        colorPalette: [
            "#117733", // dark green
            "#332288", // dark indigo
            "#88ccee", // azure
            "#cc6677", // muted rose
            "#ddcc77", // gold
            "#44aa99", // turquoise
            "#aa4499"  // magenta
        ],

        // configure the 'mods by type' stats
        modsByType: {
            // display names for each mod type (omitted mod types are shown as-is)
            labels: {
                "SMAPI": "SMAPI (C#)",
                "Pathoschild.ContentPatcher": "Content Patcher",
                "XNB": "XNB",
                "PeacefulEnd.AlternativeTextures": "Alternative Textures",
                "spacechase0.JsonAssets": "Json Assets",
                "PeacefulEnd.FashionSense": "Fashion Sense",
                "Digus.ProducerFrameworkMod": "Producer Framework Mod",
                "DIGUS.MailFrameworkMod": "Mail Framework Mod",
                "Esca.FarmTypeManager": "Farm Type Manager",
                "Cherry.ShopTileFramework": "Shop Tile Framework",
                "Platonymous.TMXLoader": "TMXL",
                "cat.betterartisangoodicons": "Better Artisan Good Icons",
                "spacechase0.DynamicGameAssets": "Dynamic Game Assets",
                "Platonymous.CustomFurniture": "Custom Furniture",
                "Platonymous.CustomMusic": "Custom Music",
                "leroymilo.FurnitureFramework": "Furniture Framework",
                "Paritee.BetterFarmAnimalVariety": "Better Farm Animal Variety",
                "other": "other (<100 packs)"
            },

            // colors for each mod type (omitted mod types cycle through the colorPalette)
            colors: {
                "other": "#c0c0c8" // silver-gray
            },

            // notable events which may affect stats, indexed by their date in mods-by-type.jsonl
            notableEvents: {
                "2021-01-01": "Stardew Valley 1.5 (21 December 2020)",
                "2021-09-01": "Stardew Valley 1.5.5 (17 August 2021)",
                "2024-04-26": "Stardew Valley 1.6 (19 March 2024)",
                "2024-11-29": "Stardew Valley 1.6.9 (04 November 2024)"
            }
        },

        // configure the 'content packs by format version' stats
        contentPacks: {
            // colors for each format version (omitted entries cycle through the colorPalette)
            colors: {},

            // notable events which may affect stats, indexed by their date in content-packs-by-format.jsonl
            notableEvents: {
                "2021-10-01": "Stardew Valley 1.5.5 (17 August 2021)",
                "2024-04-01": "Stardew Valley 1.6 (19 March 2024)",
                "2024-11-29": "Stardew Valley 1.6.9 (04 November 2024)"
            }
        },

        // configure the 'SMAPI costs' stats
        costs: {
            // colors for each service (omitted entries cycle through the colorPalette)
            colors: {
                "GitHub": "#ddcc77",         // gold
                "MongoDB": "#aa4499",        // magenta
                "Azure hosting": "#db4437",  // muted rose
                "Amazon hosting": "#332288", // dark indigo
                "Amazon SSL": "#44aa99",     // turquoise
                "Amazon domains": "#117733"   // dark green
            }
        }
    };


    /*********
    ** Set up state
    *********/
    const data = {
        modsByType: {
            rows: [],
            lastRow: {},
            xLabels: [],
            modTypes: []
        },
        newMods: {
            deltas: [],
            xLabels: []
        },
        contentPacks: {
            rows: [],
            lastRow: {},
            xLabels: [],
            versions: []
        },
        costs: {
            rows: [],
            serviceKeys: [],
            xLabels: []
        }
    };
    const charts = [];


    /*********
    ** Init app
    *********/
    app = new Vue({
        el: "#app",
        data: {
            isDataLoaded: false,
            isDataFailed: false,
            showAdvancedControls: false,

            modsByType: {
                previouslyUpdated: null,
                lastUpdated: null,
                total: 0,
                topThree: [],
                xnbPercent: "0"
            },
            latestNewMods: {
                total: 0,
                byType: [],
                forOther: 0
            },
            contentPacks: {
                previouslyUpdated: null,
                lastUpdated: null,
                total: 0,
                topVersions: []
            },
            costs: {
                lastUpdated: null,
                previousCost: 0,
                latestCost: 0,
                percentChange: 0
            }
        },
        watch: {
            showAdvancedControls: function () {
                if (this.isDataLoaded)
                    this.createChartsAsync();
            },

            isDataLoaded: function () {
                const quickNav = document.getElementById("quickNav");
                if (quickNav)
                    quickNav.hidden = !this.isDataLoaded;
            }
        },
        mounted: function () {
            this.loadAsync();
        },
        methods: {
            /**
             * Fetch the raw data files, initialize the dataset, and build the page's charts.
             */
            loadAsync: async function () {
                await this.loadDataAsync();
                await this.createChartsAsync();

                window.addEventListener("resize", () => resizeCharts(charts));
            },

            /**
             * Fetch the raw data files and initialize the dataset.
             */
            loadDataAsync: async function () {
                /*********
                ** Fetch data files
                *********/
                const [modsByTypeRows, contentPacksRows, costsRows] = await Promise.all([
                    fetchJsonLinesAsync(options.modsByTypeUri),
                    fetchJsonLinesAsync(options.contentPacksByFormatUri),
                    fetchJsonLinesAsync(options.smapiCostsUri)
                ]);
                if (!modsByTypeRows || !contentPacksRows || !costsRows) {
                    this.isDataFailed = true;
                    return;
                }


                /*********
                ** Process mods by type
                *********/
                {
                    const rows = modsByTypeRows;

                    // derive dataset info
                    const lastRow = rows[rows.length - 1];
                    data.modsByType = {
                        previouslyUpdated: formatFullDate(rows[rows.length - 2].date),
                        lastUpdated: formatFullDate(lastRow.date),

                        rows,
                        lastRow,
                        xLabels: rows.map(row => row.date),
                        modTypes: getModKeys(rows)
                    };
                    this.modsByType.previouslyUpdated = data.modsByType.previouslyUpdated;
                    this.modsByType.lastUpdated = data.modsByType.lastUpdated;

                    // summarize mods by type
                    {
                        const curData = data.modsByType;
                        const total = getSum(curData.modTypes, modType => curData.lastRow[modType]);

                        const topThree = curData.modTypes
                            .filter(isSpecificModType)
                            .slice(0, 3)
                            .map(modType => ({ label: getLabel(config.modsByType.labels, modType), percent: getPercent(curData.lastRow[modType], total) }));

                        this.modsByType.total = total;
                        this.modsByType.topThree = topThree;
                        this.modsByType.xnbPercent = getPercent(curData.lastRow["XNB"], total);
                    }

                    // assign colors for each mod type
                    assignColors(config.modsByType.colors, data.modsByType.modTypes);
                }


                /*********
                ** Calculate 'new mods this month' stats
                *********/
                {
                    const curData = data.modsByType;

                    // exclude historical manual adjustments (e.g. from mod dump rebuilds)
                    const excludeDates = new Set(["2021-03-06", "2022-01-01", "2023-05-04", "2024-08-02"]);
                    const excludeRow = row => excludeDates.has(row.date);

                    // get total new mods per month (skip first row which has no delta)
                    const totals = curData.rows.map(row => getSum(curData.modTypes, modType => row[modType]));
                    const deltas = curData.rows
                        .slice(1)
                        .map((_, i) => totals[i + 1] - totals[i])
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const filteredXLabels = curData.xLabels
                        .slice(1)
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const lastDelta = deltas[deltas.length - 1];

                    // get per-type deltas for last month
                    const lastRow = curData.lastRow;
                    const prevRow = curData.rows[curData.rows.length - 2];
                    const byType = curData.modTypes
                        .filter(isSpecificModType)
                        .map(modType => ({
                            type: modType,
                            label: getLabel(config.modsByType.labels, modType),
                            delta: (lastRow[modType] ?? 0) - (prevRow[modType] ?? 0)
                        }))
                        .sort((a, b) => b.delta - a.delta);
                    const otherDelta = (lastRow["other"] ?? 0) - (prevRow["other"] ?? 0);

                    // save values
                    data.newMods = {
                        deltas: deltas,
                        xLabels: filteredXLabels
                    };
                    this.latestNewMods = {
                        total: lastDelta,
                        byType: byType,
                        forOther: otherDelta
                    };
                }


                /*********
                ** Process content packs by format version
                *********/
                {
                    // read data
                    const rows = contentPacksRows;
                    const lastRow = rows[rows.length - 1];
                    const prevRow = rows[rows.length - 2];
                    const versions = getModKeys(rows);
                    data.contentPacks = {
                        rows,
                        lastRow,
                        xLabels: rows.map(row => row.date),
                        versions
                    };

                    // assign colors for each format version
                    assignColors(config.contentPacks.colors, versions);

                    // summarize top versions
                    const total = getSum(versions, version => lastRow[version]);
                    this.contentPacks = {
                        previouslyUpdated: formatFullDate(prevRow.date),
                        lastUpdated: formatFullDate(lastRow.date),
                        total,
                        topVersions: versions.slice(0, 5).map(version => ({
                            version,
                            count: lastRow[version] ?? 0,
                            delta: (lastRow[version] ?? 0) - (prevRow[version] ?? 0)
                        }))
                    };
                }


                /*********
                ** Process SMAPI costs
                *********/
                {
                    // read data
                    const rows = [...costsRows]; // copy since we'll be mutating it
                    const serviceKeys = [...new Set(rows.flatMap(row => Object.keys(row).filter(isDataField)))];
                    const xLabels = rows.map(row => row.date);

                    // amortize annual costs into monthly ones (so graph is more representative of average monthly costs)
                    {
                        const amortizing = [];
                        for (const row of rows) {
                            // collect new amortized amounts
                            for (const [key, value] of Object.entries(row)) {
                                if (value.amount && value.months) {
                                    amortizing.push({ key, perMonth: value.amount / value.months, months: value.months });
                                    row[key] = 0;
                                }
                            }

                            // apply amortized amounts
                            for (let i = amortizing.length - 1; i >= 0; i--) {
                                const entry = amortizing[i];
                                row[entry.key] = (row[entry.key] ?? 0) + entry.perMonth;
                                entry.months--;

                                if (entry.months < 1)
                                    amortizing.splice(i, 1);
                            }
                        }
                    }

                    // sort by descending cost in the last row (so the largest segment is at the bottom of the stacked bar)
                    serviceKeys.sort((a, b) => (rows[rows.length - 1][b] ?? 0) - (rows[rows.length - 1][a] ?? 0));

                    // set data
                    assignColors(config.costs.colors, serviceKeys);
                    data.costs = { rows, serviceKeys, xLabels };

                    // set summary
                    const prevRow = rows[rows.length - 2];
                    const lastRow = rows[rows.length - 1];
                    const previousCost = Math.round(getSum(serviceKeys, key => prevRow[key]) * 100) / 100;
                    const latestCost = Math.round(getSum(serviceKeys, key => lastRow[key]) * 100) / 100;
                    this.costs = {
                        lastUpdated: formatMonthYear(rows[rows.length - 1].date),
                        previousCost,
                        latestCost,
                        percentChange: Math.abs((latestCost - previousCost) / previousCost)
                    };
                }


                /*********
                ** Mark data ready
                *********/
                this.isDataLoaded = true;
            },

            /**
             * Create (or recreate) the charts on the page.
             */
            createChartsAsync: async function () {
                /*********
                ** Reset
                *********/
                // destroy any current charts
                for (const chart of charts)
                    chart.dispose();
                charts.length = 0;

                // wait for Vue to render chart containers if needed
                await this.$nextTick();


                /*********
                ** 'Mods by type' pie chart
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 1;

                    const chart = echarts.init(document.getElementById("modsByType"));

                    chart.setOption({
                        baseOption: {
                            series: [createPieChartSeries()],
                            timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.modsByType.notableEvents),
                            toolbox: createToolbox(false, this.showAdvancedControls)
                        },
                        options: curData.rows.map((row, i) => {
                            const isLatest = i === lastRowIndex;
                            const total = getSum(curData.modTypes, modType => row[modType]);

                            return {
                                title: {
                                    text: getTimelineTitle("Total mods by type", row.date, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                series: [
                                    {
                                        label: {
                                            formatter: entry => `${entry.name}  ${getPercent(entry.value, total)}%`
                                        },
                                        data: curData.modTypes.map(modType => ({
                                            name: getLabel(config.modsByType.labels, modType),
                                            value: row[modType] ?? 0,
                                            label: {
                                                show: !!row[modType]
                                            },
                                            itemStyle: {
                                                color: getColor(config.modsByType.colors, modType)
                                            }
                                        }))
                                    }
                                ],
                                tooltip: {
                                    formatter: entry => entry.name && entry.value
                                        ? `${entry.name}: ${entry.value.toLocaleString()} (${getPercent(entry.value, total)}%)`
                                        : null // not a slice (e.g. a timeline control)
                                },
                                animation: isLatest // don't animate previous rows, which prevents the chart from updating while playing/scrolling timeline
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'Total mods by type' line charts
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 1;

                    const chartConfigs = [
                        {
                            id: "modsByTypeOverTime",
                            title: "Mods by type over time",
                            modTypes: curData.modTypes.filter(isSpecificModType)
                        },
                        {
                            id: "modsByTypeOverTimeExcludingOutliers",
                            title: "Mods by type over time (excluding SMAPI and Content Patcher)",
                            modTypes: curData.modTypes.filter(key => isSpecificModType(key) && key !== "SMAPI" && key !== "Pathoschild.ContentPatcher")
                        }
                    ];

                    for (const chartConfig of chartConfigs) {
                        const chart = echarts.init(document.getElementById(chartConfig.id));

                        chart.setOption({
                            baseOption: {
                                legend: {
                                    show: false
                                },
                                grid: {
                                    top: 40,
                                    bottom: this.showAdvancedControls ? 100 : 60, // make room for timeline
                                    left: 60,
                                    right: 150 // make room for end labels
                                },
                                xAxis: {
                                    data: [], // overridden per-frame in `options`
                                    axisLabel: {
                                        rotate: 90,
                                        fontSize: 11,
                                        formatter: value => value.slice(0, 7)
                                    }
                                },
                                yAxis: {},
                                series: chartConfig.modTypes.map(modType => {
                                    const extendsToEnd = !!curData.lastRow[modType];
                                    const rawColor = getColor(config.modsByType.colors, modType);
                                    const color = extendsToEnd
                                        ? rawColor
                                        : hexToRgba(rawColor, 0.6);

                                    return {
                                        type: "line",
                                        name: getLabel(config.modsByType.labels, modType),
                                        data: [], // overridden per-frame in `options`
                                        color: color,
                                        lineStyle: {
                                            type: extendsToEnd ? "solid" : "dotted",
                                            width: 2
                                        },
                                        symbol: "none",
                                        endLabel: {
                                            show: true,
                                            formatter: "{a}", // {a} = name
                                            color: color
                                        },
                                        labelLayout: {
                                            moveOverlap: extendsToEnd
                                                ? "shiftY" // if the line reaches the end of the chart, use 'shiftY' layout to avoid overlapping labels
                                                : null     // if the line ends early, just display it next to the line instead
                                        }
                                    };
                                }),
                                tooltip: {
                                    trigger: "axis"
                                },
                                toolbox: createToolbox(true, this.showAdvancedControls),
                                timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.modsByType.notableEvents)
                            },
                            options: curData.rows.map((row, i) => {
                                const isLatest = i === lastRowIndex;

                                return {
                                    title: {
                                        text: getTimelineTitle(chartConfig.title, row.date, isLatest),
                                        textStyle: createTitleStyle()
                                    },
                                    xAxis: {
                                        data: curData.xLabels.slice(0, i + 1) // skip first row, since it has no previous row to delta against
                                    },
                                    series: chartConfig.modTypes.map(modType => {
                                        const data = curData.rows.slice(0, i + 1).map(row => row[modType] ?? null);

                                        return {
                                            data,
                                            endLabel: {
                                                show: !!data[data.length - 1]
                                            }
                                        };
                                    }),
                                    animation: isLatest
                                };
                            })
                        });

                        charts.push(chart);
                    }
                }


                /*********
                ** 'New mods' pie chart
                *********/
                {
                    const curData = data.modsByType;
                    const lastRowIndex = curData.rows.length - 2; // rendered rows start at row 1 (since they delta against previous row)

                    const chart = echarts.init(document.getElementById("newModsLastDelta"));

                    chart.setOption({
                        baseOption: {
                            series: [
                                {
                                    ...createPieChartSeries(),
                                    label: {
                                        formatter: entry => `${entry.name}  ${entry.percent.toFixed(1)}%`
                                    },
                                    data: [] // overridden per-frame in `options`
                                }
                            ],
                            tooltip: {
                                formatter: entry => `${entry.name}: ${entry.value.toLocaleString()} (${entry.percent.toFixed(1)}%)`
                            },
                            toolbox: createToolbox(false, this.showAdvancedControls),
                            timeline: createTimeline(
                                curData.xLabels.slice(1),
                                this.showAdvancedControls,
                                config.modsByType.notableEvents,
                                {
                                    controlStyle: {
                                        showPlayBtn: false // hide play button, since the data is too erratic for autoplay to be useful
                                    }
                                }
                            )
                        },
                        options: curData.rows.slice(1).map((row, i) => {
                            const prevRow = curData.rows[i];
                            const isLatest = i === lastRowIndex;

                            return {
                                title: {
                                    text: `New mods between ${formatFullDate(prevRow.date)} and ${formatFullDate(row.date)}`,
                                    textStyle: createTitleStyle()
                                },
                                series: [
                                    {
                                        data: curData.modTypes
                                            .map(modType => ({
                                                type: modType,
                                                delta: (row[modType] ?? 0) - (prevRow[modType] ?? 0)
                                            }))
                                            .filter(entry => entry.delta > 0)
                                            .map(entry => ({
                                                name: getLabel(config.modsByType.labels, entry.type),
                                                value: entry.delta,
                                                itemStyle: {
                                                    color: getColor(config.modsByType.colors, entry.type)
                                                }
                                            }))
                                    }
                                ],
                                animation: isLatest
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'New mods' bar chart
                *********/
                {
                    const chart = echarts.init(document.getElementById("newMods"));
                    const lastRowIndex = data.newMods.xLabels.length - 1;

                    chart.setOption({
                        baseOption: {
                            legend: {
                                show: false
                            },
                            grid: {
                                top: 40,
                                bottom: this.showAdvancedControls ? 100 : 60, // make room for timeline
                                left: 60,
                                right: 60
                            },
                            xAxis: {
                                data: data.newMods.xLabels, // base data; overridden per frame in options[]
                                axisLabel: {
                                    rotate: 90,
                                    fontSize: 11,
                                    formatter: value => value.slice(0, 7)
                                }
                            },
                            yAxis: {},
                            series: [
                                {
                                    type: "bar",
                                    data: [] // placeholder; overridden per frame in options[]
                                }
                            ],
                            tooltip: {
                                trigger: "axis"
                            },
                            toolbox: createToolbox(true, this.showAdvancedControls),
                            timeline: createTimeline(data.newMods.xLabels, this.showAdvancedControls, config.modsByType.notableEvents)
                        },
                        options: data.newMods.xLabels.map((xLabel, i) => {
                            const isLatest = i === lastRowIndex;

                            return {
                                title: {
                                    text: getTimelineTitle("New mods by month", xLabel, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                xAxis: {
                                    data: data.newMods.xLabels.slice(0, i + 1)
                                },
                                series: [
                                    {
                                        data: data.newMods.deltas.slice(0, i + 1)
                                    }
                                ],
                                animation: isLatest
                            };
                        })
                    });
                    charts.push(chart);
                }


                /*********
                ** 'Content packs by format version' pie chart
                *********/
                {
                    const curData = data.contentPacks;
                    const lastRowIndex = curData.rows.length - 1;

                    const chart = echarts.init(document.getElementById("contentPacksByFormat"));

                    chart.setOption({
                        baseOption: {
                            series: [createPieChartSeries()],
                            timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.contentPacks.notableEvents),
                            toolbox: createToolbox(false, this.showAdvancedControls)
                        },
                        options: curData.rows.map((row, i) => {
                            const isLatest = i === lastRowIndex;
                            const total = getSum(curData.versions, version => row[version]);

                            return {
                                title: {
                                    text: getTimelineTitle("Content packs by format version", row.date, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                series: [
                                    {
                                        label: {
                                            formatter: entry => `${entry.name}  ${getPercent(entry.value, total)}%`
                                        },
                                        data: curData.versions.map(version => ({
                                            name: version,
                                            value: row[version] ?? 0,
                                            label: {
                                                show: !!row[version]
                                            },
                                            itemStyle: {
                                                color: getColor(config.contentPacks.colors, version)
                                            }
                                        }))
                                    }
                                ],
                                tooltip: {
                                    formatter: entry => entry.name && entry.value
                                        ? `${entry.name}: ${entry.value.toLocaleString()} (${getPercent(entry.value, total)}%)`
                                        : null
                                },
                                animation: isLatest
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'Content packs by format version' line chart
                *********/
                {
                    // init main data
                    const curData = data.contentPacks;
                    const lastRowIndex = curData.rows.length - 1;

                    // find versions that were ever in the top five
                    const showVersionsSet = new Set();
                    for (const row of curData.rows) {
                        for (const version of [...curData.versions].sort((a, b) => (row[b] ?? 0) - (row[a] ?? 0)).slice(0, 5))
                            showVersionsSet.add(version);
                    }
                    const showVersions = Array.from(showVersionsSet);

                    const chart = echarts.init(document.getElementById("contentPacksByFormatOverTime"));

                    chart.setOption({
                        baseOption: {
                            legend: {
                                show: false
                            },
                            grid: {
                                top: 40,
                                bottom: this.showAdvancedControls ? 100 : 60, // make room for timeline
                                left: 60,
                                right: 150 // make room for end labels
                            },
                            xAxis: {
                                data: [], // overridden per-frame in `options`
                                axisLabel: {
                                    rotate: 90,
                                    fontSize: 11,
                                    formatter: value => value.slice(0, 7)
                                }
                            },
                            yAxis: {},
                            series: showVersions.map(version => {
                                return {
                                    type: "line",
                                    name: version,
                                    data: [], // overridden per-frame in `options`
                                    symbol: "none",
                                    endLabel: {
                                        formatter: "{a}" // {a} = name
                                    },
                                    labelLayout: {
                                        moveOverlap: "shiftY"
                                    }
                                };
                            }),
                            tooltip: {
                                trigger: "axis"
                            },
                            toolbox: createToolbox(true, this.showAdvancedControls),
                            timeline: createTimeline(curData.xLabels, this.showAdvancedControls, config.contentPacks.notableEvents)
                        },
                        options: curData.rows.map((row, i) => {
                            const isLatest = i === lastRowIndex;

                            return {
                                title: {
                                    text: getTimelineTitle("Content packs by format version (top five)", row.date, isLatest),
                                    textStyle: createTitleStyle()
                                },
                                xAxis: {
                                    data: curData.xLabels.slice(0, i + 1)
                                },
                                series: (() => {
                                    const topVersions = new Set(
                                        [...showVersions]
                                            .sort((a, b) => (curData.rows[i][b] ?? 0) - (curData.rows[i][a] ?? 0))
                                            .slice(0, 5)
                                    );

                                    return showVersions.map(version => {
                                        const highlight = topVersions.has(version);
                                        const rawColor = getColor(config.contentPacks.colors, version);
                                        const color = highlight ? rawColor : hexToRgba(rawColor, 0.5);
                                        const seriesData = curData.rows.slice(0, i + 1).map(r => r[version] ?? null);

                                        return {
                                            data: seriesData,
                                            color,
                                            lineStyle: {
                                                type: highlight ? "solid" : "dotted"
                                            },
                                            endLabel: {
                                                show: !!seriesData[seriesData.length - 1],
                                                color
                                            }
                                        };
                                    });
                                })(),
                                animation: isLatest
                            };
                        })
                    });

                    charts.push(chart);
                }


                /*********
                ** 'SMAPI costs over time' stacked bar chart
                *********/
                {
                    const curData = data.costs;

                    const chart = echarts.init(document.getElementById("costsOverTime"));

                    chart.setOption({
                        title: {
                            text: `SMAPI costs (${curData.xLabels[0]} through ${curData.xLabels[curData.xLabels.length - 1]})`,
                            textStyle: createTitleStyle()
                        },
                        legend: {
                            top: 25
                        },
                        grid: {
                            top: 65,
                            bottom: this.showAdvancedControls ? 80 : 60,
                            left: 60,
                            right: 20
                        },
                        xAxis: {
                            type: "category",
                            data: curData.xLabels,
                            axisLabel: {
                                rotate: 90,
                                fontSize: 11
                            }
                        },
                        yAxis: {
                            type: "value",
                            name: "USD"
                        },
                        series: curData.serviceKeys.map(key => ({
                            type: "bar",
                            name: key,
                            stack: "total",
                            data: curData.rows.map(row => Math.round((row[key] ?? 0) * 100) / 100),
                            color: getColor(config.costs.colors, key)
                        })),
                        tooltip: {
                            trigger: "axis",
                            formatter: params => {
                                const total = getSum(params, p => p.value);

                                const lines = [
                                    params[0].axisValue, // date
                                    ...params
                                        .filter(p => p.value > 0)
                                        .map(p => `${p.marker}${p.seriesName}: $${p.value.toFixed(2)}`),
                                    `<strong>Total: US$${total.toFixed(2)}</strong>`
                                ];

                                return lines.join("<br />");
                            }
                        },
                        toolbox: createToolbox(true, this.showAdvancedControls)
                    });

                    charts.push(chart);
                }
            },

            /**
             * Format a numeric change for display, with comma separators and sign.
             * @param {number} value The number to format.
             * @returns {string} Returns the formatted number.
             */
            formatDelta: function (value) {
                return value > 0
                    ? `+${value.toLocaleString()}`
                    : value.toLocaleString();
            }
        }
    });


    /*********
    ** Helper methods
    *********/
    /****
    ** Generic data
    ****/
    /**
     * Fetch and parse data from a JSONL URI.
     * @param {string} fetchUri The URI from which to fetch the JSONL data.
     * @returns {null|array<object>} Returns an array of parsed objects if the data was found, else `null`.
     */
    async function fetchJsonLinesAsync(fetchUri) {
        const response = await fetch(fetchUri);
        if (!response.ok)
            return null;

        const rawJsonLines = await response.text();
        return rawJsonLines.trim().split(/\r?\n/).filter(row => !!row).map(JSON.parse);
    }

    /**
     * Get a formatted date string.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     * @param {Intl.DateTimeFormatOptions} options The date format options.
     */
    function formatDate(date, options) {
        if (typeof date === "string")
            date = new Date(date + "T12:00"); // set time to noon to avoid any timezone conversions changing the date parts

        return new Intl.DateTimeFormat("en-GB", options).format(date);
    }

    /**
     * Get a formatted date string in the form 'dd MMM yyyy', like '31 January 2030'.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     */
    function formatFullDate(date) {
        return formatDate(date, { dateStyle: "long" });
    }

    /**
     * Get a formatted month string in the form 'Month YYYY', like 'January 2030'.
     * @param {string} monthDateStr The month date string in the form 'YYYY-MM'.
     */
    function formatMonthYear(monthDateStr) {
        return formatDate(monthDateStr + "-01", { month: "long", year: "numeric" });
    }

    /**
     * Get the display name for a data key.
     * @param {object} lookup The name dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getLabel(lookup, key) {
        return lookup[key] ?? key;
    }

    /**
     * Get the hexadecimal color code for a data key.
     * @param {object} lookup The color dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getColor(lookup, key) {
        return lookup[key] ?? "#808080";
    }

    /**
     * Get a single-decimal percentage value.
     * @param {number|null|undefined} value The value for which to get a percentage.
     * @param {number} total The denominator for the value.
     * @returns {string}
     */
    function getPercent(value, total) {
        if (!value)
            value = 0;

        return (Math.round(value / total * 1000) / 10).toString();
    }

    /**
     * Get the sum of a function over all values.
     * @param {array<string|number>} values The values for which to get a sum.
     * @param {(any) => number|null} getValue Get the value to sum for an entry.
     * @returns {number}
     */
    function getSum(values, getValue) {
        return values.reduce((sum, value) => sum + (getValue(value) ?? 0), 0);
    }

    /**
     * Get an RGBA representation of a hexadecimal color.
     * @param {string} hex The hexadecimal color code, including the '#' prefix.
     * @param {number} alpha The alpha channel, as a value between 0 (transparent) and 1 (opaque).
     * @returns
     */
    function hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }

    /**
     * Get the unique keys from a column-per-key row, sorted by their value in the latest row.
     * @param {array<object>} rows The parsed rows over time.
     * @returns {array<string>}
     */
    function getModKeys(rows) {
        const rawModTypes = rows.flatMap(row => Object.keys(row).filter(isDataField));
        const uniqueModTypes = [...new Set(rawModTypes)];

        const lastRow = rows[rows.length - 1];
        return uniqueModTypes.sort((modTypeA, modTypeB) => {
            // special case: 'other' always at the end
            if (modTypeA === "other")
                return 1;
            if (modTypeB === "other")
                return -1;

            // else sort by count descending
            const countA = lastRow[modTypeA] ?? 0;
            const countB = lastRow[modTypeB] ?? 0;
            return countB - countA;
        })
    }

    /**
     * Assign colors to each key in a color map based on the color palette.
     * @param {object} colorMap A lookup of key to color.
     * @param {array<string>} keys The keys for which to assign colors.
     */
    function assignColors(colorMap, keys) {
        let index = 0;
        for (const key of keys) {
            if (!colorMap[key])
                colorMap[key] = config.colorPalette[index++ % config.colorPalette.length];
        }
    }

    /**
     * Get whether a field name in a dated row is metadata (like a date or comment) rather than one of the actual data fields.
     * @param {string} key The data key to check.
     * @returns {boolean}
     */
    function isMetaField(key) {
        return key === "date" || key === "@comment";
    }

    /**
     * Get whether a field name in a dated row is a data value, instead of a metadata field (like a date or comment).
     * @param {string} key The data key to check.
     * @returns {boolean}
     */
    function isDataField(key) {
        return !isMetaField(key);
    }

    /****
    ** Mods-by-type data
    ****/
    /**
     * Get whether a key in a data row is a strict mod type, rather than a metadata field (like 'date' or '@comment') or 'other'.
     * @param {string} key The data key to check.
     * @returns {boolean}
     */
    function isSpecificModType(key) {
        return isDataField(key) && key !== "other";
    }

    /****
    ** Charts
    ****/
    /**
     * Get the chart title for a given timeline frame.
     * @param {string} baseTitle The base chart title.
     * @param {string|Date} date The date of the current timeline frame.
     * @param {boolean} isLatest Whether the chart is displaying the last frame on the timeline.
     * @returns {string}
     */
    function getTimelineTitle(baseTitle, date, isLatest) {
        return isLatest
            ? baseTitle
            : `${baseTitle} (${formatFullDate(date)})`;
    }

    /**
     * Create the default chart title style.
     */
    function createTitleStyle() {
        return {
            fontFamily: "Roboto",
            fontSize: 13
        };
    }

    /**
     * Create the default series configuration for a pie chart.
     * @returns {object}
     */
    function createPieChartSeries() {
        return {
            type: "pie",
            radius: "80%",
            clockwise: false // start with larger values on the left
        };
    }

    /**
     * Create the timeline options for a chart, given its x-axis labels and optional date markers.
     * @param {array<string>} xLabels The x-axis labels.
     * @param {boolean} show Whether the timeline controls should be visible.
     * @param {array<object>} markers The date markers to display on the timeline, if any. This should be a lookup of `xLabels` key to tooltip text.
     * @param {object|null} customOptions the custom ECharts timeline options to merge into the default options, if any.
     */
    function createTimeline(xLabels, show, markers, customOptions) {
        return {
            show,
            axisType: "category",
            data: xLabels.map(xLabel => {
                const marker = markers[xLabel];
                return marker
                    ? {
                        value: xLabel,
                        symbol: "diamond",
                        tooltip: {
                            formatter: () => marker
                        }
                    }
                    : {
                        value: xLabel,
                        symbol: "none"
                    };
            }),
            currentIndex: xLabels.length - 1, // default to latest row
            playInterval: 200,
            loop: false,
            realtime: true,
            label: {
                show: false
            },
            ...customOptions
        };
    }

    /**
     * Create the toolbox options for a chart.
     * @param {boolean} isLinearChart Whether the options are for a linear chart (e.g. a bar chart or line chart).
     * @param {boolean} show Whether to show advanced controls.
     */
    function createToolbox(isLinearChart, showAdvancedControls = true) {
        const toolbox = {
            feature: {
                dataView: {
                    show: showAdvancedControls,
                    readOnly: true
                },
                saveAsImage: {
                    excludeComponents: ["timeline", "toolbox"]
                }
            }
        };

        if (isLinearChart && showAdvancedControls)
            toolbox.feature.dataZoom = {};

        return toolbox;
    }

    /**
     * Resize all charts to match their container size.
     * @param {array<object>} charts The charts to resize.
     */
    function resizeCharts(charts) {
        for (const chart of charts)
            chart.resize();
    }
};
